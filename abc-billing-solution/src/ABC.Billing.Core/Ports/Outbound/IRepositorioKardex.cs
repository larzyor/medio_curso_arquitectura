using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Ports.Outbound;

public interface IRepositorioKardex
{
    /// <summary>Guarda en una sola transacción los movimientos del kardex y la auditoría del sistema legado.</summary>
    Task GuardarMigracionAsync(IReadOnlyList<MovimientoKardex> kardex, IReadOnlyList<AuditoriaMovimientoLegado> auditoria, CancellationToken ct = default);
}
