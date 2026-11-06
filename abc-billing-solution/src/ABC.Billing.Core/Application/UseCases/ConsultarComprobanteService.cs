using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Core.Application.UseCases;

public sealed class ConsultarComprobanteService(IInvoiceRepository repositorio) : IConsultarComprobanteUseCase
{
    public async Task<Invoice> ObtenerAsync(Guid id, CancellationToken ct = default)
        => await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException($"No existe el comprobante {id}.");

    public Task<IReadOnlyList<Invoice>> ListarAsync(string? serie, EstadoComprobante? estado, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
        => repositorio.ListarAsync(serie, estado, desde, hasta, ct);
}
