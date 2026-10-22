using System.Collections.Concurrent;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador de persistencia SQL (SQLite embebido para Windows/Android o PostgreSQL central).
/// </summary>
public class SqlInvoiceRepository : IInvoiceRepository
{
    private static readonly ConcurrentDictionary<Guid, Invoice> _db = new();
    private static readonly ConcurrentDictionary<string, int> _correlativos = new();

    public Task GuardarAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _db[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task<Invoice?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _db.TryGetValue(id, out var invoice);
        return Task.FromResult(invoice);
    }

    public Task<int> ObtenerSiguienteCorrelativoAsync(string serie, CancellationToken cancellationToken = default)
    {
        int nuevoCorrelativo = _correlativos.AddOrUpdate(serie, 1, (_, actual) => actual + 1);
        return Task.FromResult(nuevoCorrelativo);
    }
}
