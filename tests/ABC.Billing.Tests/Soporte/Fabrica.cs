using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Application.UseCases;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using ABC.Billing.Infrastructure.UblEngine.Generacion;

namespace ABC.Billing.Tests.Soporte;

/// <summary>Datos de prueba y servicios reales (sin dobles) para armar comprobantes y generar su XML.</summary>
internal static class Fabrica
{
    // RUC ficticios con dígito verificador Módulo 11 válido
    public const string RucEmisor = "20600000013";
    public const string RucCliente = "20123456786";

    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public static readonly CalculadoraTributos Calculadora = new(0.50m);
    public static readonly GeneradorXmlUblFactura21 Generador = new();
    public static readonly ValidadorXsdUbl Validador = new();
    public static readonly X509Certificate2 Certificado = CertificadoDePrueba.Crear(RucEmisor);
    public static FirmadorXmlDsig Firmador => new(Certificado);

    public static Emisor NuevoEmisor() => new() { Ruc = new Ruc(RucEmisor), RazonSocial = "ABC SOFTWARE CORP S.A.C.", NombreComercial = "ABC Abarrotes" };

    public static Cliente ClienteConRuc(string razon = "BODEGA EL ÑANDÚ S.A.C.") =>
        new() { TipoDocumento = TipoDocumentoIdentidad.RUC, NumeroDocumento = RucCliente, RazonSocial = razon };

    public static LineaComprobante Linea(int n, string desc, decimal cantidad, decimal valorUnitario, AfectacionIgv afectacion = AfectacionIgv.GravadoOperacionOnerosa,
        UnidadMedida unidad = UnidadMedida.NIU, SistemaIsc? isc = null, decimal tasaIsc = 0m, decimal montoFijoIsc = 0m, bool icbper = false) => new()
    {
        NumeroLinea = n, CodigoProducto = $"P{n:D3}", Descripcion = desc, UnidadMedida = unidad, Cantidad = cantidad, ValorUnitario = valorUnitario,
        AfectacionIgv = afectacion, SistemaIsc = isc, TasaIsc = tasaIsc, MontoFijoIsc = montoFijoIsc, AplicaIcbper = icbper
    };

    /// <summary>Comprobante ya calculado (sin pasar por el caso de uso): útil para probar solo la generación.</summary>
    public static Comprobante Armar(IEnumerable<LineaComprobante> lineas, TipoComprobante tipo = TipoComprobante.Factura,
        TipoOperacion operacion = TipoOperacion.VentaInterna, Cliente? cliente = null, string serie = "F001", long correlativo = 1)
    {
        var c = new Comprobante
        {
            Emisor = NuevoEmisor(), TipoComprobante = tipo, TipoOperacion = operacion, Numeracion = new SerieComprobante(serie, correlativo),
            Cliente = cliente ?? ClienteConRuc(), FechaEmision = new DateTime(2026, 11, 3, 10, 15, 0), Lineas = lineas.ToList()
        };
        Calculadora.Calcular(c);
        return c;
    }

    /// <summary>El caso completo de la especificación (sección 7.2): gravado, ISC, exonerado, inafecto, ICBPER y gratuito.</summary>
    public static Comprobante ComprobanteCompleto() => Armar(
    [
        Linea(1, "Arroz extra 5 kg", 10m, 18.00m),
        Linea(2, "Cerveza 620 ml (ISC al valor, tasa de prueba 10 %)", 6m, 5.00m, isc: SistemaIsc.AlValor, tasaIsc: 10m),
        Linea(3, "Quinua exonerada (ejemplo)", 5m, 12.00m, AfectacionIgv.ExoneradoOperacionOnerosa, UnidadMedida.KGM),
        Linea(4, "Sal de mesa inafecta (ejemplo)", 2m, 4.00m, AfectacionIgv.InafectoOperacionOnerosa),
        Linea(5, "Bolsa de plástico", 3m, 0.10m, icbper: true),
        Linea(6, "Muestra gratuita (retiro por premio)", 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio)
    ]);

    public static byte[] XmlSinFirma(Comprobante c) => Generador.GenerarXmlUbl21(c);
    public static byte[] XmlFirmado(Comprobante c) => Firmador.Firmar(XmlSinFirma(c)).Xml;

    public static XDocument Doc(byte[] xml) { using var ms = new MemoryStream(xml); return XDocument.Load(ms); }
    public static string Texto(byte[] xml) => Encoding.Latin1.GetString(xml);

    public static EmitirFacturaService ServicioDeEmision(IRepositorioComprobantes? repo = null, IRepositorioCorrelativos? series = null) => new(
        NuevoEmisor(), new SeriesPorDefecto(), Calculadora, series ?? new RepositorioCorrelativosEnMemoria(), Generador, Validador, Firmador,
        repo ?? new RepositorioComprobantesEnMemoria(), new RelojFijo(new DateTime(2026, 11, 3, 10, 15, 0)));

    public static EmitirFacturaCommand ComandoCompleto() => new(TipoComprobante.Factura, TipoOperacion.VentaInterna, null, TipoMoneda.PEN,
        new ClienteCommand(TipoDocumentoIdentidad.RUC, RucCliente, "BODEGA \"EL ÑANDÚ\" & HIJOS S.A.C. <prueba>", "Av. Alameda Perú 123"),
        [
            new ItemCommand("ARR-5KG", "Arroz extra 5 kg", UnidadMedida.NIU, 10m, 18.00m, AfectacionIgv.GravadoOperacionOnerosa),
            new ItemCommand("CER-620", "Cerveza 620 ml", UnidadMedida.NIU, 6m, 5.00m, AfectacionIgv.GravadoOperacionOnerosa, SistemaIsc.AlValor, TasaIsc: 10m),
            new ItemCommand("BOL-PLA", "Bolsa de plástico", UnidadMedida.NIU, 3m, 0.10m, AfectacionIgv.GravadoOperacionOnerosa, AplicaIcbper: true)
        ]);
}

internal sealed class RelojFijo(DateTime fecha) : IReloj { public DateTime AhoraPeru() => fecha; }
