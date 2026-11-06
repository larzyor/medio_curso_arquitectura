using System.Text;
using System.Xml.Linq;
using ABC.Billing.Infrastructure.UblEngine.Generated;

namespace ABC.Billing.UblEngine.Tests.Apoyo;

/// <summary>Lectura del XML generado para las aserciones. Las rutas se escriben como en la guía: "cac:TaxTotal/cbc:TaxAmount".</summary>
public sealed class XmlUbl
{
    public static readonly XNamespace Inv = UblXmlNs.Invoice, Cac = UblXmlNs.Cac, Cbc = UblXmlNs.Cbc, Ext = UblXmlNs.Ext;

    public byte[] Bytes { get; }
    public string Texto { get; }
    public XElement Raiz { get; }

    private XmlUbl(byte[] bytes)
    {
        Bytes = bytes;
        Texto = Encoding.Latin1.GetString(bytes);
        Raiz = XDocument.Parse(Texto).Root!;
    }

    public static XmlUbl Leer(byte[] bytes) => new(bytes);

    /// <summary>Elementos que cumplen la ruta, partiendo de <paramref name="desde"/> (por defecto, la raíz Invoice).</summary>
    public IEnumerable<XElement> Todos(string ruta, XElement? desde = null)
    {
        IEnumerable<XElement> actuales = [desde ?? Raiz];
        foreach (var paso in ruta.Split('/'))
            actuales = actuales.SelectMany(e => e.Elements(Nombre(paso)));
        return actuales;
    }

    public XElement Uno(string ruta, XElement? desde = null)
    {
        var encontrados = Todos(ruta, desde).ToList();
        Assert.True(encontrados.Count == 1, $"Se esperaba exactamente un {ruta} y hay {encontrados.Count}");
        return encontrados[0];
    }

    public string Valor(string ruta, XElement? desde = null) => Uno(ruta, desde).Value;

    public IReadOnlyList<XElement> Lineas => Todos("cac:InvoiceLine").ToList();

    /// <summary>Subtotales de tributo (cac:TaxSubtotal) de un cac:TaxTotal, indexados por código del Catálogo 05.</summary>
    public Dictionary<string, XElement> SubtotalesPorTributo(XElement taxTotal)
        => Todos("cac:TaxSubtotal", taxTotal).ToDictionary(s => Valor("cac:TaxCategory/cac:TaxScheme/cbc:ID", s));

    public static XName Nombre(string paso)
    {
        var partes = paso.Split(':');
        XNamespace ns = partes[0] switch
        {
            "cac" => Cac,
            "cbc" => Cbc,
            "ext" => Ext,
            _ => throw new ArgumentException($"Prefijo desconocido en '{paso}'")
        };
        return ns + partes[1];
    }
}
