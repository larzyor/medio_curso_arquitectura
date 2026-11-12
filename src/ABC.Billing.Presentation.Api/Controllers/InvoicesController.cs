using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Presentation.Api.DTOs;
using ABC.Billing.Presentation.Api.DTOs.Requests;
using ABC.Billing.Presentation.Api.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABC.Billing.Presentation.Api.Controllers;

/// <summary>
/// Capa MVC (controlador DELGADO): traduce HTTP ⇄ comandos y respuestas. No calcula impuestos, no asigna
/// serie ni correlativo y no conoce SUNAT: todo eso vive en los casos de uso (puertos de entrada).
/// </summary>
[ApiController]
[Route("api/v1/invoices")]
[Authorize(Roles = "emisor")]
public sealed class InvoicesController(
    IEmitirFacturaUseCase emitir,
    ITransmitirComprobanteUseCase transmitir,
    IConsultarComprobanteUseCase consultar) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Crear([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var factura = await emitir.EmitirAsync(InvoiceRequestMapper.ToCommand(request), ct);
        return CreatedAtAction(nameof(Obtener), new { id = factura.Id }, InvoiceResponse.From(factura));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceResponse>> Obtener(Guid id, CancellationToken ct)
        => InvoiceResponse.From(await consultar.ObtenerAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>> Listar(
        [FromQuery] string? serie, [FromQuery] EstadoComprobante? estado,
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
        => Ok((await consultar.ListarAsync(serie, estado, desde, hasta, ct)).Select(InvoiceResponse.From).ToList());

    /// <summary>XML UBL 2.1 firmado, en ISO-8859-1 tal como se envía a SUNAT.</summary>
    [HttpGet("{id:guid}/xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> ObtenerXml(Guid id, CancellationToken ct)
    {
        var comp = await consultar.ObtenerAsync(id, ct);
        return File(comp.XmlFirmado ?? throw new ABC.Billing.Core.Domain.ConflictoException("El comprobante aún no tiene XML firmado."),
            "application/xml; charset=ISO-8859-1", comp.NombreArchivo + ".xml");
    }

    /// <summary>sendBill. 200 = SUNAT respondió con CDR; 202 = SUNAT no disponible, queda en contingencia.</summary>
    [HttpPost("{id:guid}/transmit")]
    [ProducesResponseType(typeof(TransmisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TransmisionResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Transmitir(Guid id, CancellationToken ct)
    {
        var r = await transmitir.TransmitirAsync(id, ct);
        var cuerpo = TransmisionResponse.From(r);
        return r.Estado == EstadoComprobante.EnContingencia ? Accepted(cuerpo) : Ok(cuerpo);
    }

    /// <summary>Constancia de Recepción (ZIP con R-&lt;nombre&gt;.xml) guardada tras el envío.</summary>
    [HttpGet("{id:guid}/cdr")]
    [Produces("application/zip")]
    public async Task<IActionResult> ObtenerCdr(Guid id, CancellationToken ct)
    {
        var comp = await consultar.ObtenerAsync(id, ct);
        return comp.CdrZip is { Length: > 0 } zip
            ? File(zip, "application/zip", $"R-{comp.NombreArchivo}.zip")
            : throw new ABC.Billing.Core.Domain.NoEncontradoException("El comprobante todavía no tiene CDR.");
    }
}
