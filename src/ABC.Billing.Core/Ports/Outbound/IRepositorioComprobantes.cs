using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Ports.Outbound;

public interface IRepositorioComprobantes
{
    Task GuardarAsync(Comprobante comprobante, CancellationToken ct = default);
    Task<Comprobante?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Comprobante>> ListarAsync(string? serie, EstadoComprobante? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);
}
