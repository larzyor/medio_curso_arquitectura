using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Generacion;

public class FirmaDigitalTests
{
    private static byte[] NuevoXml() => Fabrica.XmlSinFirma(Fabrica.ComprobanteCompleto());

    [Fact]
    public void La_firma_queda_dentro_de_ExtensionContent_y_es_valida()
    {
        var firmado = Fabrica.Firmador.Firmar(NuevoXml());
        var contenido = Fabrica.Doc(firmado.Xml).Root!.Element(Fabrica.Ext + "UBLExtensions")!
            .Elements(Fabrica.Ext + "UBLExtension").Single().Element(Fabrica.Ext + "ExtensionContent")!;
        Assert.Equal("Signature", contenido.Elements().Single().Name.LocalName);
        Assert.True(FirmadorXmlDsig.Verificar(firmado.Xml));
    }

    [Fact]
    public void Alterar_el_XML_despues_de_firmar_invalida_la_firma()
    {
        var firmado = Fabrica.Firmador.Firmar(NuevoXml());
        var alterado = System.Text.Encoding.Latin1.GetBytes(Fabrica.Texto(firmado.Xml).Replace("Arroz extra 5 kg", "Arroz extra 50 kg"));
        Assert.False(FirmadorXmlDsig.Verificar(alterado));
    }

    [Fact]
    public void Guarda_el_digest_y_el_documento_firmado_conserva_la_codificacion()
    {
        var firmado = Fabrica.Firmador.Firmar(NuevoXml());
        Assert.False(string.IsNullOrEmpty(firmado.DigestValue));
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>", Fabrica.Texto(firmado.Xml));
    }

    [Fact]
    public void La_firma_referencia_al_documento_completo_y_hay_una_sola_extension()
    {
        System.Xml.Linq.XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        var raiz = Fabrica.Doc(Fabrica.Firmador.Firmar(NuevoXml()).Xml).Root!;
        var extensiones = raiz.Element(Fabrica.Ext + "UBLExtensions")!.Elements(Fabrica.Ext + "UBLExtension").ToList();
        Assert.Single(extensiones);                                                           // Manual 3.2.a: una sola extensión
        var firma = extensiones[0].Descendants(ds + "Signature").Single();
        Assert.Equal("SignABC", firma.Attribute("Id")!.Value);                                // el mismo Id que cac:Signature
        var referencia = firma.Descendants(ds + "Reference").Single();
        Assert.Equal("", referencia.Attribute("URI")!.Value);                                 // documento completo (Manual 3.2.b)
        Assert.Contains(referencia.Descendants(ds + "Transform"), t => t.Attribute("Algorithm")!.Value.EndsWith("enveloped-signature"));
        Assert.Equal("#SignABC", raiz.Element(Fabrica.Cac + "Signature")!.Descendants(Fabrica.Cbc + "URI").Single().Value);
        Assert.NotNull(firma.Descendants(ds + "X509Certificate").SingleOrDefault());          // certificado del firmante incluido
    }

    [Fact]
    public void Dos_firmas_del_mismo_documento_verifican_por_separado()
    {
        var a = Fabrica.Firmador.Firmar(NuevoXml());
        var b = new FirmadorXmlDsig(Fabrica.Certificado, AlgoritmoFirma.RsaSha256).Firmar(NuevoXml());
        Assert.True(FirmadorXmlDsig.Verificar(a.Xml));
        Assert.True(FirmadorXmlDsig.Verificar(b.Xml));
        Assert.True(a.DigestValue != b.DigestValue, "SHA1 y SHA256 dan digests distintos");
    }
}
