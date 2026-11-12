using System.ComponentModel.DataAnnotations;

namespace ABC.Billing.Presentation.Api.DTOs.Requests;

/// <summary>Cuerpo de POST /api/v1/invoices. Los catálogos viajan con los códigos SUNAT (strings).</summary>
public sealed class CreateInvoiceRequest
{
    /// <summary>Catálogo 01: "01" factura, "03" boleta.</summary>
    [Required] public string TipoComprobante { get; set; } = "01";
    /// <summary>Catálogo 51 (cbc:ProfileID): "0101" venta interna, "0102" exportación...</summary>
    public string TipoOperacion { get; set; } = "0101";
    /// <summary>Opcional (F### o B###). El correlativo NUNCA lo envía el cliente: lo asigna el servidor.</summary>
    public string? Serie { get; set; }
    /// <summary>Catálogo 02: PEN, USD, EUR.</summary>
    public string Moneda { get; set; } = "PEN";
    [Required] public ClienteDto Cliente { get; set; } = new();
    [Required, MinLength(1)] public List<ItemDto> Items { get; set; } = [];
}

public sealed class ClienteDto
{
    /// <summary>Catálogo 06: "6" RUC, "1" DNI, "4" CE, "7" pasaporte, "0" sin RUC no domiciliado.</summary>
    [Required] public string TipoDocumento { get; set; } = "6";
    [Required, StringLength(15)] public string NumeroDocumento { get; set; } = "";
    [Required, StringLength(250)] public string RazonSocial { get; set; } = "";
    [StringLength(250)] public string? Direccion { get; set; }
}

public sealed class ItemDto
{
    [StringLength(30)] public string CodigoProducto { get; set; } = "";
    [Required, StringLength(250)] public string Descripcion { get; set; } = "";
    /// <summary>Catálogo 03 (UN/ECE Rec. 20): NIU, KGM, LTR, ZZ...</summary>
    [Required] public string UnidadMedida { get; set; } = "NIU";
    [Range(0.0001, 999999999)] public decimal Cantidad { get; set; }
    /// <summary>Valor unitario SIN impuestos (en gratuitas, el valor referencial).</summary>
    [Range(0.0000001, 999999999)] public decimal ValorUnitario { get; set; }
    /// <summary>Catálogo 07: "10" gravado oneroso, "20" exonerado, "30" inafecto, "40" exportación, "11".."16" retiros...</summary>
    [Required] public string AfectacionIgv { get; set; } = "10";
    /// <summary>Catálogo 08 (SOLO sistema de ISC): "01" al valor, "02" monto fijo, "03" precio de venta al público.</summary>
    public string? SistemaIsc { get; set; }
    /// <summary>Tasa del ISC en % (sistema al valor).</summary>
    [Range(0, 1000)] public decimal TasaIsc { get; set; }
    [Range(0, 999999999)] public decimal MontoFijoIsc { get; set; }
    /// <summary>true si el ítem es una bolsa de plástico afecta al ICBPER (Catálogo 05, código 7152).</summary>
    public bool AplicaIcbper { get; set; }
}

public sealed class LegacyMovementDto
{
    [Required] public string Fecha { get; set; } = "";
    [Range(0, 999999999)] public decimal Ingreso { get; set; }
    [Range(0, 999999999)] public decimal Salida { get; set; }
    [Required] public string Producto { get; set; } = "";
    public string NroDocumento { get; set; } = "";
}

public sealed class MigrateLegacyRequest
{
    [Required, MinLength(1), MaxLength(5000)] public List<LegacyMovementDto> Registros { get; set; } = [];
}
