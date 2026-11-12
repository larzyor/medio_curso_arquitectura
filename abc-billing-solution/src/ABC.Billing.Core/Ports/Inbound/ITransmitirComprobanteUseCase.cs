using ABC.Billing.Core.Application.Commands;

namespace ABC.Billing.Core.Ports.Inbound;

public interface ITransmitirComprobanteUseCase
{
    /// <summary>Envía el comprobante firmado a SUNAT/OSE (sendBill) y actualiza su estado con el CDR.</summary>
    Task<TransmisionResultado> TransmitirAsync(Guid id, CancellationToken ct = default);
}
