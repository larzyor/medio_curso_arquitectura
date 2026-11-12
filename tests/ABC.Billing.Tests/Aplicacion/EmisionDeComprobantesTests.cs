using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Aplicacion;

public class EmisionDeComprobantesTests
{
    [Fact]
    public async Task Emitir_deja_el_comprobante_calculado_firmado_y_con_XML_valido()
    {
        var repo = new RepositorioComprobantesEnMemoria();
        var servicio = Fabrica.ServicioDeEmision(repo);
        var c = await servicio.EmitirAsync(Fabrica.ComandoCompleto());

        Assert.Equal("F001-1", c.SerieNumero);
        Assert.Equal(EstadoComprobante.Firmado, c.Estado);
        Assert.NotNull(c.XmlFirmado);
        Assert.True(FirmadorXmlDsig.Verificar(c.XmlFirmado!));
        Fabrica.Validador.Validar(c.XmlFirmado!);
        Assert.NotNull(await repo.ObtenerAsync(c.Id));
        Assert.Equal("20600000013-01-F001-1", c.NombreArchivo);
    }

    [Fact]
    public async Task El_correlativo_lo_asigna_el_servidor_y_avanza()
    {
        var servicio = Fabrica.ServicioDeEmision();
        var a = await servicio.EmitirAsync(Fabrica.ComandoCompleto());
        var b = await servicio.EmitirAsync(Fabrica.ComandoCompleto());
        Assert.Equal("F001-1", a.SerieNumero);
        Assert.Equal("F001-2", b.SerieNumero);
    }

    [Fact]
    public async Task Una_boleta_toma_la_serie_B_por_defecto()
    {
        var cmd = Fabrica.ComandoCompleto() with
        {
            TipoComprobante = TipoComprobante.BoletaDeVenta,
            Cliente = new ClienteCommand(TipoDocumentoIdentidad.DNI, "45678912", "JUAN PEREZ RAMOS", null)
        };
        var c = await Fabrica.ServicioDeEmision().EmitirAsync(cmd);
        Assert.Equal("B001-1", c.SerieNumero);
    }

    [Fact]
    public async Task Cada_serie_lleva_su_propio_correlativo()
    {
        var servicio = Fabrica.ServicioDeEmision();
        var a = await servicio.EmitirAsync(Fabrica.ComandoCompleto() with { SerieSolicitada = "F002" });
        var b = await servicio.EmitirAsync(Fabrica.ComandoCompleto());
        Assert.Equal("F002-1", a.SerieNumero);
        Assert.Equal("F001-1", b.SerieNumero);
    }

    [Fact]
    public async Task El_correlativo_es_atomico_bajo_concurrencia()
    {
        var repo = new RepositorioCorrelativosEnMemoria();
        var tomados = await Task.WhenAll(Enumerable.Range(0, 500).Select(_ => Task.Run(() => repo.SiguienteAsync(TipoComprobante.Factura, "F009"))));
        Assert.Equal(500, tomados.Distinct().Count());
        Assert.Equal(1, tomados.Min());
        Assert.Equal(500, tomados.Max());
    }

    [Fact]
    public async Task Una_factura_exige_cliente_con_RUC()
    {
        var cmd = Fabrica.ComandoCompleto() with { Cliente = new ClienteCommand(TipoDocumentoIdentidad.DNI, "12345678", "PEDRO", null) };
        await Assert.ThrowsAsync<DominioValidationException>(() => Fabrica.ServicioDeEmision().EmitirAsync(cmd));
    }

    [Fact]
    public async Task Una_factura_no_puede_usar_serie_B()
        => await Assert.ThrowsAsync<DominioValidationException>(() => Fabrica.ServicioDeEmision().EmitirAsync(Fabrica.ComandoCompleto() with { SerieSolicitada = "B001" }));

    [Fact]
    public async Task Un_RUC_de_cliente_invalido_se_rechaza()
    {
        var cmd = Fabrica.ComandoCompleto() with { Cliente = new ClienteCommand(TipoDocumentoIdentidad.RUC, "20600000014", "X", null) };
        await Assert.ThrowsAsync<DominioValidationException>(() => Fabrica.ServicioDeEmision().EmitirAsync(cmd));
    }

    [Fact]
    public async Task Una_boleta_de_700_o_mas_exige_identificar_al_cliente()
    {
        var cmd = new EmitirFacturaCommand(TipoComprobante.BoletaDeVenta, TipoOperacion.VentaInterna, null, TipoMoneda.PEN,
            new ClienteCommand(TipoDocumentoIdentidad.NoDomiciliadoSinRuc, "-", "CLIENTE VARIOS", null),
            [new ItemCommand("X", "Mercadería", UnidadMedida.NIU, 10m, 100m, AfectacionIgv.GravadoOperacionOnerosa)]);
        await Assert.ThrowsAsync<DominioValidationException>(() => Fabrica.ServicioDeEmision().EmitirAsync(cmd));
    }

    [Fact]
    public async Task Sin_items_no_se_emite()
        => await Assert.ThrowsAsync<DominioValidationException>(() => Fabrica.ServicioDeEmision().EmitirAsync(Fabrica.ComandoCompleto() with { Items = [] }));

    [Fact]
    public async Task Un_error_de_dominio_no_consume_correlativo_visible_ni_guarda_nada()
    {
        var repo = new RepositorioComprobantesEnMemoria();
        await Assert.ThrowsAsync<DominioValidationException>(() =>
            Fabrica.ServicioDeEmision(repo).EmitirAsync(Fabrica.ComandoCompleto() with { Items = [Fabrica.ComandoCompleto().Items[0] with { Cantidad = 0 }] }));
        Assert.Empty(await repo.ListarAsync(null, null, null, null));
    }
}
