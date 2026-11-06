using ABC.Billing.Infrastructure.UblEngine.Generated;

namespace ABC.Billing.Infrastructure.UblEngine.Model;

public static class UblNs
{
    public const string Invoice = UblXmlNs.Invoice;
    public const string Cac = UblXmlNs.Cac;
    public const string Cbc = UblXmlNs.Cbc;
    public const string Ext = UblXmlNs.Ext;
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>Id de la firma: lo comparten cac:Signature/cbc:ID, su URI "#..." y ds:Signature/@Id.</summary>
    public const string SignatureId = "SignABC";

    // URN de los catálogos SUNAT usados en los atributos list*/scheme*
    public const string Agency = "PE:SUNAT";
    public static string Catalogo(string nn) => $"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo{nn}";
}
