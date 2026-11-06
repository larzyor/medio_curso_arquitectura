namespace ABC.Billing.Infrastructure.UblEngine.Model;

public static class UblNs
{
    public const string Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public const string Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public const string Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>Id de la firma: lo comparten cac:Signature/cbc:ID, su URI "#..." y ds:Signature/@Id.</summary>
    public const string SignatureId = "SignABC";

    // URN de los catálogos SUNAT usados en los atributos list*/scheme*
    public const string Agency = "PE:SUNAT";
    public static string Catalogo(string nn) => $"urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo{nn}";
}
