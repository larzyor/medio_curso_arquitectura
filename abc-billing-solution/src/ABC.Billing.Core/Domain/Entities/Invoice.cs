using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.ValueObjects;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>Raíz agregada del comprobante electrónico (factura o boleta). serie_numero SUNAT = cbc:ID UBL.</summary>
public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Factura;
    public TipoOperacion TipoOperacion { get; set; } = TipoOperacion.VentaInterna;
    public DocumentSeries Series { get; set; } = null!;
    public string SerieNumero => Series.SerieNumero;

    public required Emisor Emisor { get; set; }
    public Customer Cliente { get; set; } = null!;
    public DateTime FechaEmision { get; set; }
    public TipoMoneda Moneda { get; set; } = TipoMoneda.PEN;
    public List<InvoiceLine> Items { get; set; } = [];

    // ----- Totales calculados -----
    public List<TaxSubtotal> Tributos { get; set; } = [];
    public decimal TotalOperacionesGravadas { get; set; }      // valor de venta sin ISC
    public decimal TotalOperacionesExoneradas { get; set; }
    public decimal TotalOperacionesInafectas { get; set; }
    public decimal TotalExportacion { get; set; }
    public decimal TotalOperacionesGratuitas { get; set; }     // valor referencial; NO se cobra
    public decimal BaseImponibleIgv { get; set; }              // gravadas + ISC
    public decimal TotalIgv { get; set; }
    public decimal TotalIsc { get; set; }
    public decimal TotalIcbper { get; set; }
    public decimal TotalIgvGratuitas { get; set; }             // informativo; excluido del total a pagar
    public decimal TotalValorVenta { get; set; }               // cbc:LineExtensionAmount
    public decimal TotalPrecioVenta { get; set; }              // cbc:TaxInclusiveAmount
    public decimal TotalTributos { get; set; }                 // cac:TaxTotal/cbc:TaxAmount
    public decimal ImporteTotalPagar { get; set; }             // cbc:PayableAmount

    // ----- Ciclo de vida y trazabilidad SUNAT -----
    public EstadoComprobante Estado { get; set; } = EstadoComprobante.Pendiente;
    public byte[]? XmlFirmado { get; set; }
    public string? DigestValue { get; set; }
    public byte[]? CdrZip { get; set; }
    public string? CdrRespuestaCodigo { get; set; }            // "0" = aceptado
    public string? CdrDescripcion { get; set; }
    public List<string> CdrObservaciones { get; set; } = [];

    public string NombreArchivo => NombreArchivoSunat.Comprobante(Emisor.Ruc, TipoComprobante, Series);
}
