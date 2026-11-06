using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Ports.Outbound;

public interface IInvoiceRepository
{
    Task GuardarAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> ListarAsync(string? serie, EstadoComprobante? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default);
}
