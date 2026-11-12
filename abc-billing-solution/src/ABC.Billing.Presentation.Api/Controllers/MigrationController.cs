using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Presentation.Api.DTOs;
using ABC.Billing.Presentation.Api.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABC.Billing.Presentation.Api.Controllers;

[ApiController]
[Route("api/v1/migration")]
[Authorize(Roles = "admin")]
public sealed class MigrationController(IMigrarDatosLegadosUseCase migrar) : ControllerBase
{
    /// <summary>Migra fecha, ingreso, salida, producto y nro_documento del software básico al kardex, con trazabilidad.</summary>
    [HttpPost("legacy-stock")]
    [ProducesResponseType(typeof(MigracionResultado), StatusCodes.Status200OK)]
    public async Task<ActionResult<MigracionResultado>> MigrarStock([FromBody] MigrateLegacyRequest request, CancellationToken ct)
        => Ok(await migrar.MigrarLoteAsync(InvoiceRequestMapper.ToRecords(request), ct));
}
