using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>
/// Representa al cliente o receptor de la factura/boleta electrónica.
/// Mapeo a cac:AccountingCustomerParty en UBL 2.1.
/// </summary>
public class Customer
{
    public TipoDocumentoIdentidad TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? Direccion { get; set; }

    public Customer() { }

    public Customer(TipoDocumentoIdentidad tipoDoc, string numDoc, string razonSocial, string? direccion = null)
    {
        TipoDocumento = tipoDoc;
        NumeroDocumento = numDoc;
        RazonSocial = razonSocial;
        Direccion = direccion;
    }
}
