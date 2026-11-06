using System.Collections.Concurrent;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.Persistence.InMemory;

public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly ConcurrentDictionary<Guid, Invoice> _datos = new();

    public Task GuardarAsync(Invoice invoice, CancellationToken ct = default) { _datos[invoice.Id] = invoice; return Task.CompletedTask; }

    public Task<Invoice?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_datos.TryGetValue(id, out var inv) ? inv : null);

    public Task<IReadOnlyList<Invoice>> ListarAsync(string? serie, EstadoComprobante? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        IEnumerable<Invoice> q = _datos.Values;
        if (!string.IsNullOrWhiteSpace(serie)) q = q.Where(i => i.Series.Serie.Equals(serie, StringComparison.OrdinalIgnoreCase));
        if (estado is not null) q = q.Where(i => i.Estado == estado);
        if (desde is not null) q = q.Where(i => DateOnly.FromDateTime(i.FechaEmision) >= desde);
        if (hasta is not null) q = q.Where(i => DateOnly.FromDateTime(i.FechaEmision) <= hasta);
        return Task.FromResult<IReadOnlyList<Invoice>>(q.OrderBy(i => i.Series.Serie).ThenBy(i => i.Series.Correlativo).ToList());
    }
}

/// <summary>Correlativo atómico por (tipo, serie): AddOrUpdate es seguro ante concurrencia.</summary>
public sealed class InMemorySerieCorrelativoRepository : ISerieCorrelativoRepository
{
    private readonly ConcurrentDictionary<(TipoComprobante, string), long> _ultimos = new();

    public Task<long> SiguienteAsync(TipoComprobante tipo, string serie, CancellationToken ct = default)
        => Task.FromResult(_ultimos.AddOrUpdate((tipo, serie.ToUpperInvariant()), 1, (_, actual) => actual + 1));
}

public sealed class InMemoryKardexRepository : IKardexRepository
{
    private readonly List<KardexMovement> _kardex = [];
    private readonly List<LegacyMovementAudit> _auditoria = [];
    private readonly object _candado = new();

    public IReadOnlyList<KardexMovement> Kardex { get { lock (_candado) return _kardex.ToList(); } }
    public IReadOnlyList<LegacyMovementAudit> Auditoria { get { lock (_candado) return _auditoria.ToList(); } }

    public Task GuardarMigracionAsync(IReadOnlyList<KardexMovement> kardex, IReadOnlyList<LegacyMovementAudit> auditoria, CancellationToken ct = default)
    {
        lock (_candado) { _kardex.AddRange(kardex); _auditoria.AddRange(auditoria); }
        return Task.CompletedTask;
    }
}

public sealed class RelojPeru : IReloj
{
    public DateTime AhoraPeru() => DateTime.UtcNow.AddHours(-5); // Perú: UTC-5 sin horario de verano
}
