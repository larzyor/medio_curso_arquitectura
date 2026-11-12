using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Domain.Services;

/// <summary>
/// Catálogo 14 – Códigos de "otros conceptos tributarios", usados en el esquema anterior (UBL 2.0) como
/// <c>sac:AdditionalMonetaryTotal/cbc:ID</c> para el total por tipo de operación.
/// En UBL 2.1 estos totales viajan en <c>cac:TaxTotal</c> (tributos 1000, 9996, 9997, 9998) y <c>cac:LegalMonetaryTotal</c>;
/// por eso NO se escriben en el XML 2.1: aquí sirven para reportes, el Registro de Ventas 14.1 y la API.
/// </summary>
public enum CodigoTotalOperacion
{
    Gravadas = 1001,
    Inafectas = 1002,
    Exoneradas = 1003,
    Gratuitas = 1004,
    Fise = 3001
}

/// <summary>Totales del comprobante agrupados con los códigos 1001–1004 del Catálogo 14 (FISE 3001 no se calcula: no aplica a este alcance).</summary>
public static class TotalesPorTipoOperacion
{
    public static IReadOnlyDictionary<CodigoTotalOperacion, decimal> De(Comprobante c) => new Dictionary<CodigoTotalOperacion, decimal>
    {
        [CodigoTotalOperacion.Gravadas] = c.TotalOperacionesGravadas,
        [CodigoTotalOperacion.Inafectas] = c.TotalOperacionesInafectas,
        [CodigoTotalOperacion.Exoneradas] = c.TotalOperacionesExoneradas,
        [CodigoTotalOperacion.Gratuitas] = c.TotalOperacionesGratuitas
    };
}
