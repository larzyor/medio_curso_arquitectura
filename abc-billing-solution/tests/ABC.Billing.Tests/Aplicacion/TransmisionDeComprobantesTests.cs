using System.IO.Compression;
using System.Text;
using ABC.Billing.Core.Application.UseCases;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using ABC.Billing.Infrastructure.SunatSoap;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Aplicacion;

public class TransmisionDeComprobantesTests
{
    private static string CdrXml(int codigo, string descripcion, params string[] notas) => $"""
        <?xml version="1.0" encoding="ISO-8859-1" standalone="no"?>
        <ar:ApplicationResponse xmlns:ar="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
        <cbc:ID>201200000230061</cbc:ID>
        {string.Join("\n", notas.Select(n => $"<cbc:Note>{n}</cbc:Note>"))}
        <cac:DocumentResponse><cac:Response><cbc:ReferenceID>F001-1</cbc:ReferenceID><cbc:ResponseCode>{codigo}</cbc:ResponseCode><cbc:Description>{descripcion}</cbc:Description></cac:Response></cac:DocumentResponse>
        </ar:ApplicationResponse>
        """;

    private static byte[] ZipDelCdr(string xml)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) { using var s = z.CreateEntry("R-20600000013-01-F001-1.xml").Open(); s.Write(Encoding.Latin1.GetBytes(xml)); }
        return ms.ToArray();
    }

    private sealed class SunatSimulado(Func<CdrSunat> respuesta) : IClienteSunat
    {
        public int Envios { get; private set; }
        public Task<CdrSunat> EnviarComprobanteAsync(string nombre, byte[] xml, CancellationToken ct = default) { Envios++; return Task.FromResult(respuesta()); }
        public Task<CdrSunat> ConsultarCdrAsync(string ruc, string tipo, string serie, long numero, CancellationToken ct = default) => Task.FromResult(respuesta());
    }

    private static async Task<(RepositorioComprobantesEnMemoria Repo, Guid Id)> UnaFacturaFirmada()
    {
        var repo = new RepositorioComprobantesEnMemoria();
        var c = await Fabrica.ServicioDeEmision(repo).EmitirAsync(Fabrica.ComandoCompleto());
        return (repo, c.Id);
    }

    [Fact]
    public async Task Codigo_0_sin_notas_deja_el_comprobante_Aceptado()
    {
        var (repo, id) = await UnaFacturaFirmada();
        var cdr = LectorCdr.ParsearZip(ZipDelCdr(CdrXml(0, "La Factura numero F001-1, ha sido aceptada")));
        var r = await new TransmitirComprobanteService(repo, new SunatSimulado(() => cdr)).TransmitirAsync(id);
        Assert.Equal(EstadoComprobante.Aceptado, r.Estado);
        Assert.Equal(0, r.CodigoRespuesta);
    }

    [Fact]
    public async Task Codigo_0_con_notas_4000_deja_Aceptado_con_observaciones()
    {
        var (repo, id) = await UnaFacturaFirmada();
        var cdr = LectorCdr.ParsearZip(ZipDelCdr(CdrXml(0, "aceptada", "4031 - Debe indicar el nombre comercial", "4001 - El numero de RUC del receptor no existe.")));
        var r = await new TransmitirComprobanteService(repo, new SunatSimulado(() => cdr)).TransmitirAsync(id);
        Assert.Equal(EstadoComprobante.AceptadoConObservaciones, r.Estado);
        Assert.Equal(2, r.Observaciones.Count);
    }

    [Fact]
    public async Task Un_codigo_de_rechazo_deja_Rechazado_y_ya_no_se_puede_reenviar()
    {
        var (repo, id) = await UnaFacturaFirmada();
        var cdr = LectorCdr.ParsearZip(ZipDelCdr(CdrXml(2047, "Es obligatorio al menos un AdditionalMonetaryTotal")));
        var servicio = new TransmitirComprobanteService(repo, new SunatSimulado(() => cdr));
        Assert.Equal(EstadoComprobante.Rechazado, (await servicio.TransmitirAsync(id)).Estado);
        await Assert.ThrowsAsync<ConflictoException>(() => servicio.TransmitirAsync(id));   // la numeración queda consumida (Manual 4.1)
    }

    [Fact]
    public async Task Un_comprobante_aceptado_no_se_reenvia()
    {
        var (repo, id) = await UnaFacturaFirmada();
        var sunat = new SunatSimulado(() => LectorCdr.ParsearZip(ZipDelCdr(CdrXml(0, "aceptada"))));
        var servicio = new TransmitirComprobanteService(repo, sunat);
        await servicio.TransmitirAsync(id);
        await Assert.ThrowsAsync<ConflictoException>(() => servicio.TransmitirAsync(id));
        Assert.Equal(1, sunat.Envios);
    }

    [Fact]
    public async Task Si_SUNAT_no_esta_disponible_queda_en_contingencia_con_el_XML_intacto()
    {
        var (repo, id) = await UnaFacturaFirmada();
        var antes = (await repo.ObtenerAsync(id))!.XmlFirmado!.ToArray();
        var r = await new TransmitirComprobanteService(repo, new SunatSimulado(() => throw new SunatNoDisponibleException("sin conexión"))).TransmitirAsync(id);
        Assert.Equal(EstadoComprobante.EnContingencia, r.Estado);
        Assert.Equal(antes, (await repo.ObtenerAsync(id))!.XmlFirmado);
    }

    [Fact]
    public async Task Un_comprobante_en_contingencia_se_puede_reintentar()
    {
        var (repo, id) = await UnaFacturaFirmada();
        await new TransmitirComprobanteService(repo, new SunatSimulado(() => throw new SunatNoDisponibleException("x"))).TransmitirAsync(id);
        var ok = new SunatSimulado(() => LectorCdr.ParsearZip(ZipDelCdr(CdrXml(0, "aceptada"))));
        Assert.Equal(EstadoComprobante.Aceptado, (await new TransmitirComprobanteService(repo, ok).TransmitirAsync(id)).Estado);
    }

    [Fact]
    public async Task Una_excepcion_de_SUNAT_se_propaga_y_no_cambia_el_estado()
    {
        var (repo, id) = await UnaFacturaFirmada();
        var servicio = new TransmitirComprobanteService(repo, new SunatSimulado(() => throw new SunatExcepcionException(1033, "ZIP malformado")));
        await Assert.ThrowsAsync<SunatExcepcionException>(() => servicio.TransmitirAsync(id));
        Assert.Equal(EstadoComprobante.Firmado, (await repo.ObtenerAsync(id))!.Estado);
    }

    [Fact]
    public async Task Un_comprobante_inexistente_da_no_encontrado()
        => await Assert.ThrowsAsync<NoEncontradoException>(() =>
            new TransmitirComprobanteService(new RepositorioComprobantesEnMemoria(), new SunatSimulado(() => throw new InvalidOperationException())).TransmitirAsync(Guid.NewGuid()));
}
