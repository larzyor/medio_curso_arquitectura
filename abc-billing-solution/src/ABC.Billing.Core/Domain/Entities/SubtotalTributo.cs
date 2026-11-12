using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>Subtotal por tributo del encabezado (cac:TaxTotal/cac:TaxSubtotal). Tributo = Catálogo 05.</summary>
public sealed class SubtotalTributo
{
    public required TributoSunat Tributo { get; init; }
    public decimal? BaseImponible { get; init; }
    public decimal Monto { get; init; }
    /// <summary>Solo ICBPER: cantidad de bolsas (cbc:BaseUnitMeasure) y tarifa por bolsa (cbc:PerUnitAmount).</summary>
    public decimal? CantidadBolsas { get; init; }
    public decimal? TarifaPorBolsa { get; init; }
}
