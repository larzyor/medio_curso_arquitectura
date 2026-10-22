using System.ComponentModel.DataAnnotations;

namespace ABC.Billing.Presentation.Api.DTOs.Requests;

/// <summary>
/// Contrato de entrada (Spec de Entrada) para emitir un comprobante.
/// Permite especificar 'serie_numero' directamente o 'serie' + 'correlativo'.
/// </summary>
public class CreateInvoiceRequest
{
    [Required]
    [RegularExpression("^(01|03|07|08)$", ErrorMessage = "Catálogo 01: 01=Factura, 03=Boleta.")]
    public string TipoComprobante { get; set; } = "01";

    /// <summary>
    /// Identificador oficial peruano (ejemplo: F001-00000001).
    /// </summary>
    public string? SerieNumero { get; set; }

    [Required]
    public ClienteDto Cliente { get; set; } = null!;

    [Required]
    [MinLength(1, ErrorMessage = "Debe incluir al menos un ítem.")]
    public List<ItemDto> Items { get; set; } = [];
}

public class ClienteDto
{
    [Required]
    public string TipoDocumento { get; set; } = "6"; // 6 = RUC, 1 = DNI

    [Required]
    public string NumeroDocumento { get; set; } = string.Empty;

    [Required]
    public string RazonSocial { get; set; } = string.Empty;

    public string? Direccion { get; set; }
}

public class ItemDto
{
    [Required]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    public string UnidadMedida { get; set; } = "NIU"; // Catálogo 03

    [Range(0.001, double.MaxValue)]
    public decimal Cantidad { get; set; }

    [Range(0.0, double.MaxValue)]
    public decimal ValorUnitario { get; set; } // Sin IGV

    public string TipoAfectacionIgv { get; set; } = "10"; // Catálogo 07: 10 = Gravado

    public bool AplicaIcbper { get; set; } = false; // Impuesto a la bolsa plástica
}
