using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.UblEngine;

namespace ABC.Billing.Infrastructure.Cryptography;

public enum AlgoritmoFirma { RsaSha1, RsaSha256 }

/// <summary>
/// Firma XMLDSig "enveloped" del comprobante completo (Manual del Programador, numeral 3.2): la firma queda en
/// ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent y, una vez firmado, el documento no puede modificarse.
/// El certificado debe ser X.509 v3 con el RUC en el campo OU, vigente y comunicado a SUNAT (numeral 3.1).
/// El algoritmo por defecto (RSA-SHA1) es el que muestran los ejemplos de SUNAT; puede cambiarse a RSA-SHA256.
/// </summary>
public sealed class FirmadorXmlDsig(X509Certificate2 certificado, AlgoritmoFirma algoritmo = AlgoritmoFirma.RsaSha1) : IFirmadorDigital
{
    public XmlFirmado Firmar(byte[] xml)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        using (var ms = new MemoryStream(xml)) doc.Load(ms); // respeta la codificación declarada (ISO-8859-1)

        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("ext", UblNs.Ext);
        var contenido = doc.SelectSingleNode("//ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent", nsm) as XmlElement
            ?? throw new InvalidOperationException("El XML no tiene ext:ExtensionContent para alojar la firma.");

        using var rsa = certificado.GetRSAPrivateKey() ?? throw new InvalidOperationException("El certificado no tiene clave privada RSA.");
        var firmado = new SignedXml(doc) { SigningKey = rsa };
        firmado.Signature.Id = UblNs.SignatureId;
        firmado.SignedInfo!.SignatureMethod = algoritmo == AlgoritmoFirma.RsaSha1 ? SignedXml.XmlDsigRSASHA1Url : SignedXml.XmlDsigRSASHA256Url;

        var referencia = new Reference("") { DigestMethod = algoritmo == AlgoritmoFirma.RsaSha1 ? SignedXml.XmlDsigSHA1Url : SignedXml.XmlDsigSHA256Url };
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        firmado.AddReference(referencia);

        var info = new KeyInfo();
        info.AddClause(new KeyInfoX509Data(certificado));
        firmado.KeyInfo = info;
        firmado.ComputeSignature();

        contenido.AppendChild(doc.ImportNode(firmado.GetXml(), true));
        return new XmlFirmado(Guardar(doc), Convert.ToBase64String(referencia.DigestValue!));
    }

    /// <summary>Verifica la firma embebida (la usan las pruebas y un eventual control previo al envío).</summary>
    public static bool Verificar(byte[] xmlFirmado)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        using (var ms = new MemoryStream(xmlFirmado)) doc.Load(ms);
        var nodo = doc.GetElementsByTagName("Signature", UblNs.Ds);
        if (nodo.Count != 1) return false;
        var sx = new SignedXml(doc);
        sx.LoadXml((XmlElement)nodo[0]!);
        return sx.CheckSignature();
    }

    private static byte[] Guardar(XmlDocument doc)
    {
        using var ms = new MemoryStream();
        ms.Write(Encoding.Latin1.GetBytes(UblEngine.Generacion.GeneradorXmlUblFactura21.DeclaracionXml));
        using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = Encoding.Latin1, OmitXmlDeclaration = true, Indent = false }))
            doc.DocumentElement!.WriteTo(w);
        return ms.ToArray();
    }
}
