namespace ABC.Billing.Core.Ports.Outbound;

public sealed record XmlFirmado(byte[] Xml, string DigestValue);

public interface IFirmadorDigital
{
    /// <summary>Firma XMLDSig (enveloped) dentro de ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent.</summary>
    XmlFirmado Firmar(byte[] xml);
}
