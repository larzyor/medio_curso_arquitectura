using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.ValueObjects;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>
/// Entidad raíz agregada (Aggregate Root) del comprobante electrónico.
/// </summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TipoComprobante TipoComprobante { get; set; }
    public DocumentSeries Series { get; set; } = null!;
    
    /// <summary>
    /// Identificador peruano que el profesor mencionó: 'serie_numero'
    /// En UBL 2.1 se mapea directamente al elemento '<cbc:ID>'.
    /// </summary>
    public string SerieNumero => Series.SerieNumero;

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow.AddHours(-5); // UTC-5 Hora Perú
    public TipoMoneda Moneda { get; set; } = TipoMoneda.PEN;

    public Customer Cliente { get; set; } = null!;
    public List<InvoiceLine> Items { get; set; } = [];
    public List<TaxSubtotal> Tributos { get; set; } = [];

    // Totales calculados
    public decimal TotalOperacionesGravadas { get; set; }
    public decimal TotalOperacionesExoneradas { get; set; }
    public decimal TotalOperacionesInafectas { get; set; }
    public decimal TotalIgv { get; set; }
    public decimal TotalIcbper { get; set; }
    public decimal ImporteTotalPagar { get; set; }

    // Estados de envío y trazabilidad SUNAT
    public string Estado { get; set; } = "PENDIENTE";
    public string? DigestValue { get; set; } // Hash SHA-256 de la firma
    public string? SignatureValue { get; set; }
    public string? CdrXmlBase64 { get; set; }
    public string? CdrRespuestaCodigo { get; set; } // "0" = Aceptado
}
