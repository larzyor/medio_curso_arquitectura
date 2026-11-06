using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Ports.Inbound;

public interface IEmitirFacturaUseCase
{
    /// <summary>Asigna serie-correlativo, calcula tributos, genera el XML UBL 2.1, lo valida contra el XSD, lo firma y lo guarda.</summary>
    Task<Invoice> EmitirAsync(EmitirFacturaCommand comando, CancellationToken ct = default);
}
