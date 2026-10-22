using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Ports.Inbound;

public interface IEmitirFacturaUseCase
{
    Task<Invoice> EmitirYTransmitirAsync(Invoice invoice, CancellationToken cancellationToken = default);
}

public interface IMigrarDatosLegadosUseCase
{
    Task<int> MigrarLoteAsync(IEnumerable<LegacyMovementRecord> registros, CancellationToken cancellationToken = default);
}

public record LegacyMovementRecord(
    string Fecha,
    decimal Ingreso,
    decimal Salida,
    string Producto,
    string NroDocumento
);
