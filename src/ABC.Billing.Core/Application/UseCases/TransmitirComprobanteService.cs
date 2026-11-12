using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Core.Application.UseCases;

public sealed class TransmitirComprobanteService(IRepositorioComprobantes repositorio, IClienteSunat sunat) : ITransmitirComprobanteUseCase
{
    public async Task<TransmisionResultado> TransmitirAsync(Guid id, CancellationToken ct = default)
    {
        var comp = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException($"No existe el comprobante {id}.");

        if (comp.Estado is EstadoComprobante.Aceptado or EstadoComprobante.AceptadoConObservaciones)
            throw new ConflictoException("El comprobante ya fue aceptado por SUNAT.");
        if (comp.Estado == EstadoComprobante.Rechazado)
            throw new ConflictoException("El comprobante fue rechazado: su numeración queda utilizada (Manual 4.1). Emita uno nuevo corrigiendo el error.");
        if (comp.XmlFirmado is null)
            throw new ConflictoException("El comprobante aún no tiene XML firmado.");

        try
        {
            var cdr = await sunat.EnviarComprobanteAsync(comp.NombreArchivo, comp.XmlFirmado, ct);
            comp.CdrZip = cdr.CdrZip;
            comp.CdrRespuestaCodigo = cdr.CodigoRespuesta.ToString();
            comp.CdrDescripcion = cdr.Descripcion;
            comp.CdrObservaciones = cdr.Observaciones.ToList();
            comp.Estado = ClasificadorCodigosSunat.Clasificar(cdr.CodigoRespuesta) switch
            {
                ResultadoSunat.Aceptado when cdr.Observaciones.Count > 0 => EstadoComprobante.AceptadoConObservaciones,
                ResultadoSunat.Aceptado => EstadoComprobante.Aceptado,
                _ => EstadoComprobante.Rechazado
            };
        }
        catch (SunatNoDisponibleException)
        {
            // Sin conexión: queda en cola de contingencia para reintentar; el XML firmado ya está guardado.
            comp.Estado = EstadoComprobante.EnContingencia;
            await repositorio.GuardarAsync(comp, ct);
            return new TransmisionResultado(comp.Estado, null, "SUNAT no disponible: comprobante en cola de contingencia.", []);
        }

        await repositorio.GuardarAsync(comp, ct);
        return new TransmisionResultado(comp.Estado, int.Parse(comp.CdrRespuestaCodigo!), comp.CdrDescripcion ?? "", comp.CdrObservaciones);
    }
}
