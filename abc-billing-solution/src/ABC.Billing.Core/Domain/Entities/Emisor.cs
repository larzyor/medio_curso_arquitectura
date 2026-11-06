using ABC.Billing.Core.Domain.ValueObjects;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>Empresa emisora (ABC Software Corp). Alimenta cac:AccountingSupplierParty.</summary>
public sealed class Emisor
{
    public required RucNumber Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public string? NombreComercial { get; init; }

    /// <summary>Código de establecimiento anexo del RUC (4 dígitos). "0000" = domicilio fiscal.</summary>
    public string CodigoEstablecimiento { get; init; } = "0000";
}
