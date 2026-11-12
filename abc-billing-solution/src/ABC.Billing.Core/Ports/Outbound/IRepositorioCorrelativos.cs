using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Ports.Outbound;

/// <summary>
/// Asigna el siguiente correlativo de una serie de forma ATÓMICA (el cliente nunca elige el correlativo).
/// SQL: UPDATE serie SET ultimo = ultimo + 1 WHERE ... RETURNING ultimo (una sola sentencia).
/// </summary>
public interface IRepositorioCorrelativos
{
    Task<long> SiguienteAsync(TipoComprobante tipo, string serie, CancellationToken ct = default);
}
