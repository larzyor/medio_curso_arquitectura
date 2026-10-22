using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>
/// Representa una línea o ítem de la factura.
/// Mapeo a cac:InvoiceLine en UBL-Invoice-2.1.xsd.
/// </summary>
public class InvoiceLine
{
    public int LineNumber { get; set; }
    public string CodigoProducto { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "NIU"; // Catálogo 03: NIU = Unidad, KGM = Kilogramo
    public decimal Cantidad { get; set; }
    public decimal ValorUnitario { get; set; } // Sin IGV
    public decimal PrecioUnitario { get; set; } // Con IGV

    // Tributos
    public AfectacionIgv AfectacionIgv { get; set; } = AfectacionIgv.GravadoOperacionOnerosa;
    public decimal MontoIgv { get; set; }
    public bool AplicaIcbper { get; set; } // Impuesto a la bolsa de plástico
    public decimal MontoIcbper { get; set; } // Cantidad de bolsas * S/ 0.50

    public decimal SubtotalSinImpuestos => Cantidad * ValorUnitario;
    public decimal TotalLinea => SubtotalSinImpuestos + MontoIgv + MontoIcbper;
}
