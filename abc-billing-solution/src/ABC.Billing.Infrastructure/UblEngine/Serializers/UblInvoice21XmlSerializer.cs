using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.UblEngine.Dominio;

namespace ABC.Billing.Infrastructure.UblEngine.Serializers;

/// <summary>
/// Adaptador de salida del puerto <see cref="IUblXmlGenerator"/>: convierte el comprobante del dominio en el XML
/// UBL 2.1 de SUNAT. La traducción está escrita en el lenguaje del dominio (<see cref="ComprobanteUbl"/>) y llena las
/// clases generadas desde UBL-Invoice-2.1.xsd (UblEngine/Generated). Salida en ISO-8859-1 (Manual del Programador 1.4.c).
/// </summary>
public sealed class UblInvoice21XmlSerializer : IUblXmlGenerator
{
    public const string DeclaracionXml = ComprobanteUbl.DeclaracionXml;

    public byte[] GenerarXmlUbl21(Invoice invoice) => ComprobanteUbl.Desde(invoice).ComoXml();
}
