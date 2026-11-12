using System.IO.Compression;
using System.Xml.Linq;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.SunatSoap;

/// <summary>Lee la Constancia de Recepción (ApplicationResponse, Anexo 1 del Manual): código, descripción y observaciones.</summary>
public static class LectorCdr
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public static CdrSunat ParsearXml(byte[] cdrXml, byte[]? cdrZip = null)
    {
        XDocument doc;
        using (var ms = new MemoryStream(cdrXml)) doc = XDocument.Load(ms);

        var respuesta = doc.Descendants(Cac + "DocumentResponse").Descendants(Cac + "Response").FirstOrDefault()
            ?? throw new InvalidDataException("El CDR no contiene cac:DocumentResponse/cac:Response.");
        int codigo = int.Parse(respuesta.Element(Cbc + "ResponseCode")?.Value.Trim() ?? throw new InvalidDataException("CDR sin ResponseCode."));
        string descripcion = respuesta.Element(Cbc + "Description")?.Value.Trim() ?? "";
        var notas = doc.Root!.Elements(Cbc + "Note").Select(n => n.Value.Trim()).Where(n => n.Length > 0).ToList();
        return new CdrSunat(codigo, descripcion, notas, cdrZip ?? []);
    }

    /// <summary>El CDR llega en un ZIP con el archivo R-&lt;nombre enviado&gt;.xml.</summary>
    public static CdrSunat ParsearZip(byte[] zip)
    {
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var entrada = archivo.Entries.FirstOrDefault(e => e.Name.StartsWith("R-", StringComparison.OrdinalIgnoreCase)
                                                            && e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("El ZIP de respuesta no contiene el CDR (R-*.xml).");
        using var s = entrada.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ParsearXml(ms.ToArray(), zip);
    }
}
