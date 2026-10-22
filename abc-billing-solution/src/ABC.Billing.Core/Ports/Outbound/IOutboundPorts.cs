using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Ports.Outbound;

public interface IInvoiceRepository
{
    Task GuardarAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task<Invoice?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> ObtenerSiguienteCorrelativoAsync(string serie, CancellationToken cancellationToken = default);
}

public interface IUblXmlGenerator
{
    /// <summary>
    /// Genera la cadena XML estructurada bajo UBL-Invoice-2.1.xsd
    /// Mapea el campo 'serie_numero' al nodo '<cbc:ID>'.
    /// </summary>
    string GenerarXmlUbl21(Invoice invoice);
}

public interface IDigitalSigner
{
    /// <summary>
    /// Aplica la firma digital XAdES-BES al XML y retorna el XML firmado junto con el DigestValue (Hash).
    /// </summary>
    (string XmlFirmado, string DigestValue) FirmarXml(string xmlRaw, string certPassword);
}

public interface ISunatOseClient
{
    /// <summary>
    /// Envía el archivo ZIP conteniendo el XML firmado mediante el Web Service SOAP sendBill.
    /// Retorna el archivo CDR (Constancia de Recepción) devuelto por SUNAT/OSE.
    /// </summary>
    Task<(string CdrBase64, string CodigoRespuesta, string MensajeRespuesta)> EnviarComprobanteAsync(
        string rucEmisor, 
        string tipoComprobante, 
        string serieNumero, 
        byte[] zipFirmado, 
        CancellationToken cancellationToken = default);
}
