namespace ABC.Billing.Core.Domain.Enums;

/// <summary>
/// Catálogo 01 SUNAT - Código de Tipo de Documento
/// </summary>
public enum TipoComprobante
{
    Factura = 01,               // "01"
    BoletaDeVenta = 03,         // "03"
    NotaDeCredito = 07,         // "07"
    NotaDeDebito = 08           // "08"
}

/// <summary>
/// Catálogo 02 SUNAT - Código de Tipo de Moneda (ISO 4217)
/// </summary>
public enum TipoMoneda
{
    PEN, // Soles
    USD  // Dólares Americanos
}

/// <summary>
/// Catálogo 06 SUNAT - Código de Tipo de Documento de Identidad
/// </summary>
public enum TipoDocumentoIdentidad
{
    SinDocumento = 0,
    DNI = 1,
    CarnetExtranjeria = 4,
    RUC = 6,
    Pasaporte = 7
}

/// <summary>
/// Catálogo 07 SUNAT - Códigos de Tipo de Afectación al IGV
/// </summary>
public enum AfectacionIgv
{
    GravadoOperacionOnerosa = 10,
    GravadoRetiroPremio = 11,
    ExoneradoOperacionOnerosa = 20,
    InafectoOperacionOnerosa = 30,
    Exportacion = 40
}

/// <summary>
/// Catálogo 08 SUNAT - Códigos de Tipos de Tributos (PUNTO CLAVE UBL 2.1)
/// Mapeo a cac:TaxCategory / cac:TaxScheme
/// </summary>
public static class CodigoTributo
{
    public const string IGV = "1000";       // Nombre: "IGV", Tipo: "VAT"
    public const string ISC = "2000";       // Nombre: "ISC", Tipo: "EXC"
    public const string ICBPER = "7152";    // Nombre: "ICBPER", Tipo: "OTH" (Bolsa de plástico)
    public const string Exonerado = "9997"; // Nombre: "EXO", Tipo: "VAT"
    public const string Inafecto = "9998";  // Nombre: "INA", Tipo: "FRE"
    public const string Gratuito = "9996";  // Nombre: "GRA", Tipo: "FRE"
}
