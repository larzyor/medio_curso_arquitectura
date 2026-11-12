using System.Collections.Concurrent;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.Persistence.InMemory;

public sealed class RepositorioComprobantesEnMemoria : IRepositorioComprobantes
{
    private readonly ConcurrentDictionary<Guid, Comprobante> _datos = new();

    public Task GuardarAsync(Comprobante comprobante, CancellationToken ct = default) { _datos[comprobante.Id] = comprobante; return Task.CompletedTask; }

    public Task<Comprobante?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_datos.TryGetValue(id, out var comp) ? comp : null);

    public Task<IReadOnlyList<Comprobante>> ListarAsync(string? serie, EstadoComprobante? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        IEnumerable<Comprobante> q = _datos.Values;
        if (!string.IsNullOrWhiteSpace(serie)) q = q.Where(i => i.Numeracion.Serie.Equals(serie, StringComparison.OrdinalIgnoreCase));
        if (estado is not null) q = q.Where(i => i.Estado == estado);
        if (desde is not null) q = q.Where(i => DateOnly.FromDateTime(i.FechaEmision) >= desde);
        if (hasta is not null) q = q.Where(i => DateOnly.FromDateTime(i.FechaEmision) <= hasta);
        return Task.FromResult<IReadOnlyList<Comprobante>>(q.OrderBy(i => i.Numeracion.Serie).ThenBy(i => i.Numeracion.Correlativo).ToList());
    }
}

/// <summary>Correlativo atómico por (tipo, serie): AddOrUpdate es seguro ante concurrencia.</summary>
public sealed class RepositorioCorrelativosEnMemoria : IRepositorioCorrelativos
{
    private readonly ConcurrentDictionary<(TipoComprobante, string), long> _ultimos = new();

    public Task<long> SiguienteAsync(TipoComprobante tipo, string serie, CancellationToken ct = default)
        => Task.FromResult(_ultimos.AddOrUpdate((tipo, serie.ToUpperInvariant()), 1, (_, actual) => actual + 1));
}

public sealed class RepositorioKardexEnMemoria : IRepositorioKardex
{
    private readonly List<MovimientoKardex> _kardex = [];
    private readonly List<AuditoriaMovimientoLegado> _auditoria = [];
    private readonly object _candado = new();

    public IReadOnlyList<MovimientoKardex> Kardex { get { lock (_candado) return _kardex.ToList(); } }
    public IReadOnlyList<AuditoriaMovimientoLegado> Auditoria { get { lock (_candado) return _auditoria.ToList(); } }

    public Task GuardarMigracionAsync(IReadOnlyList<MovimientoKardex> kardex, IReadOnlyList<AuditoriaMovimientoLegado> auditoria, CancellationToken ct = default)
    {
        lock (_candado) { _kardex.AddRange(kardex); _auditoria.AddRange(auditoria); }
        return Task.CompletedTask;
    }
}

public sealed class RelojPeru : IReloj
{
    public DateTime AhoraPeru() => DateTime.UtcNow.AddHours(-5); // Perú: UTC-5 sin horario de verano
}
