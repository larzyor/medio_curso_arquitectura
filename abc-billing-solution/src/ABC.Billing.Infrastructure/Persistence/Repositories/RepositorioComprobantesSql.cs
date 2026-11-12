using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.Persistence.Repositories;

/// <summary>
/// ESQUELETO del adaptador SQL (fase 2). Implementa el mismo puerto que el repositorio en memoria, por lo que
/// reemplazarlo no toca el dominio ni los casos de uso. El esquema propuesto está en docs/sql/001_schema.sql.
/// </summary>
public sealed class RepositorioComprobantesSql : IRepositorioComprobantes
{
    private const string Pendiente = "Fase 2: implementar con ADO.NET/EF Core sobre el esquema de docs/sql/001_schema.sql.";

    public Task GuardarAsync(Comprobante comprobante, CancellationToken ct = default) => throw new NotImplementedException(Pendiente);
    public Task<Comprobante?> ObtenerAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException(Pendiente);
    public Task<IReadOnlyList<Comprobante>> ListarAsync(string? serie, EstadoComprobante? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
        => throw new NotImplementedException(Pendiente);
}
