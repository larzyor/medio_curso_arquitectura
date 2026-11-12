namespace ABC.Billing.Core.Ports.Outbound;

public sealed record CdrSunat(int CodigoRespuesta, string Descripcion, IReadOnlyList<string> Observaciones, byte[] CdrZip);

public interface IClienteSunat
{
    /// <summary>sendBill (síncrono): envía el ZIP con el XML firmado y devuelve el CDR.</summary>
    Task<CdrSunat> EnviarComprobanteAsync(string nombreArchivoSinExtension, byte[] xmlFirmado, CancellationToken ct = default);

    /// <summary>getStatusCdr: recupera el CDR de una factura o nota ya enviada.</summary>
    Task<CdrSunat> ConsultarCdrAsync(string rucEmisor, string tipoComprobante, string serie, long numero, CancellationToken ct = default);
}
