using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Core.Application.UseCases;

public sealed class TransmitirComprobanteService(IInvoiceRepository repositorio, ISunatOseClient sunat) : ITransmitirComprobanteUseCase
{
    public async Task<TransmisionResultado> TransmitirAsync(Guid id, CancellationToken ct = default)
    {
        var inv = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException($"No existe el comprobante {id}.");

        if (inv.Estado is EstadoComprobante.Aceptado or EstadoComprobante.AceptadoConObservaciones)
            throw new ConflictoException("El comprobante ya fue aceptado por SUNAT.");
        if (inv.Estado == EstadoComprobante.Rechazado)
            throw new ConflictoException("El comprobante fue rechazado: su numeración queda utilizada (Manual 4.1). Emita uno nuevo corrigiendo el error.");
        if (inv.XmlFirmado is null)
            throw new ConflictoException("El comprobante aún no tiene XML firmado.");

        try
        {
            var cdr = await sunat.EnviarComprobanteAsync(inv.NombreArchivo, inv.XmlFirmado, ct);
            inv.CdrZip = cdr.CdrZip;
            inv.CdrRespuestaCodigo = cdr.CodigoRespuesta.ToString();
            inv.CdrDescripcion = cdr.Descripcion;
            inv.CdrObservaciones = cdr.Observaciones.ToList();
            inv.Estado = ClasificadorCodigosSunat.Clasificar(cdr.CodigoRespuesta) switch
            {
                ResultadoSunat.Aceptado when cdr.Observaciones.Count > 0 => EstadoComprobante.AceptadoConObservaciones,
                ResultadoSunat.Aceptado => EstadoComprobante.Aceptado,
                _ => EstadoComprobante.Rechazado
            };
        }
        catch (SunatNoDisponibleException)
        {
            // Sin conexión: queda en cola de contingencia para reintentar; el XML firmado ya está guardado.
            inv.Estado = EstadoComprobante.EnContingencia;
            await repositorio.GuardarAsync(inv, ct);
            return new TransmisionResultado(inv.Estado, null, "SUNAT no disponible: comprobante en cola de contingencia.", []);
        }

        await repositorio.GuardarAsync(inv, ct);
        return new TransmisionResultado(inv.Estado, int.Parse(inv.CdrRespuestaCodigo!), inv.CdrDescripcion ?? "", inv.CdrObservaciones);
    }
}
