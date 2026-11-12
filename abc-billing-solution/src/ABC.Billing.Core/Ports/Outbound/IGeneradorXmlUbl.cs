using ABC.Billing.Core.Domain.Entities;

namespace ABC.Billing.Core.Ports.Outbound;

public interface IGeneradorXmlUbl
{
    /// <summary>Genera el XML UBL 2.1 en ISO-8859-1 (codificación que pide el Manual del Programador).</summary>
    byte[] GenerarXmlUbl21(Comprobante comprobante);
}
