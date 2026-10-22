namespace ABC.Billing.Core.Domain.Entities;

/// <summary>
/// Desglose de tributos a nivel de línea o resumen global.
/// Mapeo directo a cac:TaxSubtotal / cac:TaxScheme según Catálogo 08 de SUNAT.
/// </summary>
public class TaxSubtotal
{
    public string CodigoTributo { get; set; } = string.Empty; // Catálogo 08 (ej: "1000" para IGV, "7152" para ICBPER)
    public string NombreTributo { get; set; } = string.Empty; // "IGV", "ICBPER", "EXO"
    public string CodigoInternacional { get; set; } = string.Empty; // "VAT", "OTH", "FRE"
    public decimal BaseImponible { get; set; }
    public decimal Porcentaje { get; set; } // Ej: 18.00 para IGV
    public decimal MontoTributo { get; set; }
}
