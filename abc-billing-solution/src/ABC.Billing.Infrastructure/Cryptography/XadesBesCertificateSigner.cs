using System.Security.Cryptography;
using System.Text;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.Cryptography;

/// <summary>
/// Adaptador para la aplicación de firma digital con certificado PKCS#12 (XAdES-BES).
/// </summary>
public class XadesBesCertificateSigner : IDigitalSigner
{
    public (string XmlFirmado, string DigestValue) FirmarXml(string xmlRaw, string certPassword)
    {
        // En un entorno de producción, aquí se carga el archivo .pfx con X509Certificate2
        // y se computa el nodo <ds:Signature> con CanonicalizationMethod.
        // Para la especificación técnica simulamos el cálculo del DigestValue con SHA-256:
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(xmlRaw));
        string digestValue = Convert.ToBase64String(hashBytes);

        string firmaXml = $@"<ds:Signature xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">
  <ds:SignedInfo>
    <ds:Reference>
      <ds:DigestValue>{digestValue}</ds:DigestValue>
    </ds:Reference>
  </ds:SignedInfo>
  <ds:SignatureValue>MIIE...[Firma_Digital_XAdES_BES]...</ds:SignatureValue>
</ds:Signature>";

        string xmlFirmado = xmlRaw.Replace("<ext:ExtensionContent />", $"<ext:ExtensionContent>{firmaXml}</ext:ExtensionContent>");
        return (xmlFirmado, digestValue);
    }
}
