namespace ABC.Billing.Core.Domain.Enums;

/// <summary>
/// Catálogo 05 – Códigos de tipos de tributos y otros conceptos (cac:TaxScheme: ID, Name, TaxTypeCode).
/// Estos son los códigos 1000, 2000, 7152, 9997... (NO son del Catálogo 08).
/// CategoriaUbl = UN/ECE 5305 (cac:TaxCategory/cbc:ID): S, E, O, Z, G.
/// Los nombres (IGV, ISC, EXONERADO, INAFECTO, GRATUITO...) son los de la Guía XML Factura UBL 2.1 de SUNAT; el ICBPER (7152) y el IVAP (1016) vienen de la RS 150-2019.
/// </summary>
public sealed record TributoSunat(string Codigo, string Nombre, string CodigoInternacional, string CategoriaUbl)
{
    public static readonly TributoSunat Igv = new("1000", "IGV", "VAT", "S");
    public static readonly TributoSunat Ivap = new("1016", "IVAP", "VAT", "S");
    public static readonly TributoSunat Isc = new("2000", "ISC", "EXC", "S");
    public static readonly TributoSunat Icbper = new("7152", "ICBPER", "OTH", "S");
    public static readonly TributoSunat Exportacion = new("9995", "EXPORTACIÓN", "FRE", "G");
    public static readonly TributoSunat Gratuito = new("9996", "GRATUITO", "FRE", "Z");
    public static readonly TributoSunat Exonerado = new("9997", "EXONERADO", "VAT", "E");
    public static readonly TributoSunat Inafecto = new("9998", "INAFECTO", "FRE", "O");
    public static readonly TributoSunat Otros = new("9999", "OTROS CONCEPTOS DE PAGO", "OTH", "S");

    public static IReadOnlyList<TributoSunat> Todos { get; } =
        [Igv, Ivap, Isc, Icbper, Exportacion, Gratuito, Exonerado, Inafecto, Otros];
}

/// <summary>Catálogo 52 – Códigos de leyendas (cbc:Note/@languageLocaleID).</summary>
public static class Leyenda
{
    public const string MontoEnLetras = "1000";
    public const string TransferenciaGratuita = "1002";
    public const string ComprobantePercepcion = "2000";
    public const string BienesAmazonia = "2001";
    public const string ServiciosAmazonia = "2002";
    public const string ContratosConstruccionAmazonia = "2003";
    public const string AgenciaViajePaqueteTuristico = "2004";
    public const string VentaEmisorItinerante = "2005";
    public const string OperacionSujetaADetraccion = "2006";
    public const string CodigoInternoDelSoftware = "3000";
}
