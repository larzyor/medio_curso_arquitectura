using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Ports.Outbound;

public interface IKardexRepository
{
    /// <summary>Guarda en una sola transacción los movimientos del kardex y la auditoría del sistema legado.</summary>
    Task GuardarMigracionAsync(IReadOnlyList<KardexMovement> kardex, IReadOnlyList<LegacyMovementAudit> auditoria, CancellationToken ct = default);
}
