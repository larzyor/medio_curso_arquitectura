using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.SunatSoap;

/// <param name="Endpoint">billService de SUNAT/OSE (beta: https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService).</param>
/// <param name="ConsultaEndpoint">billConsultService (getStatusCdr).</param>
/// <param name="UsuarioSol">Usuario SOL secundario. WS-Security UsernameToken: Username = RUC + usuario, Password = clave SOL.</param>
public sealed record OpcionesSunat(string Endpoint, string ConsultaEndpoint, string RucEmisor, string UsuarioSol, string ClaveSol);

/// <summary>
/// Adaptador SOAP hacia SUNAT (Manual del Programador, cap. 2): sendBill síncrono y getStatusCdr.
/// NOTA: escrito según el manual y probado con CDR de ejemplo; no se ejecutó contra el servicio beta de SUNAT.
/// </summary>
public sealed class ClienteSoapSunat(HttpClient http, OpcionesSunat opciones) : IClienteSunat
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Ser = "http://service.sunat.gob.pe";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    public async Task<CdrSunat> EnviarComprobanteAsync(string nombreSinExtension, byte[] xmlFirmado, CancellationToken ct = default)
    {
        var zip = CrearZip(nombreSinExtension, xmlFirmado);
        var cuerpo = new XElement(Ser + "sendBill",
            new XElement("fileName", nombreSinExtension + ".zip"),
            new XElement("contentFile", Convert.ToBase64String(zip)));

        var respuesta = await EnviarAsync(opciones.Endpoint, cuerpo, ct);
        var contenido = respuesta.Descendants().FirstOrDefault(e => e.Name.LocalName == "applicationResponse")?.Value
            ?? throw new InvalidDataException("La respuesta de sendBill no contiene applicationResponse.");
        return LectorCdr.ParsearZip(Convert.FromBase64String(contenido));
    }

    public async Task<CdrSunat> ConsultarCdrAsync(string rucEmisor, string tipoComprobante, string serie, long numero, CancellationToken ct = default)
    {
        var cuerpo = new XElement(Ser + "getStatusCdr",
            new XElement("rucComprobante", rucEmisor), new XElement("tipoComprobante", tipoComprobante),
            new XElement("serieComprobante", serie), new XElement("numeroComprobante", numero));

        var respuesta = await EnviarAsync(opciones.ConsultaEndpoint, cuerpo, ct);
        var contenido = respuesta.Descendants().FirstOrDefault(e => e.Name.LocalName == "content")?.Value;
        var mensaje = respuesta.Descendants().FirstOrDefault(e => e.Name.LocalName == "statusMessage")?.Value ?? "";
        if (string.IsNullOrWhiteSpace(contenido))
            throw new NoEncontradoException($"SUNAT no devolvió CDR: {mensaje}");
        return LectorCdr.ParsearZip(Convert.FromBase64String(contenido));
    }

    /// <summary>ZIP con una carpeta "dummy" vacía y el XML, ambos con el mismo nombre que el ZIP (Manual 1.3).</summary>
    public static byte[] CrearZip(string nombreSinExtension, byte[] xml)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("dummy/");
            var entrada = zip.CreateEntry(nombreSinExtension + ".xml");
            using var s = entrada.Open();
            s.Write(xml);
        }
        return ms.ToArray();
    }

    public XDocument ConstruirSobre(XElement cuerpo) => new(new XElement(Soap + "Envelope",
        new XAttribute(XNamespace.Xmlns + "soapenv", Soap),
        new XAttribute(XNamespace.Xmlns + "ser", Ser),
        new XAttribute(XNamespace.Xmlns + "wsse", Wsse),
        new XElement(Soap + "Header",
            new XElement(Wsse + "Security",
                new XElement(Wsse + "UsernameToken",
                    new XElement(Wsse + "Username", opciones.RucEmisor + opciones.UsuarioSol),
                    new XElement(Wsse + "Password", opciones.ClaveSol)))),
        new XElement(Soap + "Body", cuerpo)));

    private async Task<XDocument> EnviarAsync(string url, XElement cuerpo, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(ConstruirSobre(cuerpo).ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        peticion.Headers.Add("SOAPAction", "\"\"");

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(peticion, ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new SunatNoDisponibleException("No fue posible comunicarse con SUNAT/OSE.", e);
        }

        using (resp)
        {
            var texto = await resp.Content.ReadAsStringAsync(ct);
            XDocument? doc = null;
            try { doc = XDocument.Parse(texto); } catch (System.Xml.XmlException) { }

            var fault = doc?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault is not null)
            {
                // soap-env:Server.0135 / Client.1033 ...: el número es el código de error del Manual (4.1).
                var faultCode = fault.Elements().FirstOrDefault(e => e.Name.LocalName == "faultcode")?.Value ?? "";
                var faultString = fault.Elements().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value ?? "Excepción de SUNAT";
                var m = Regex.Match(faultCode, @"\.(\d{4})\b");
                throw new SunatExcepcionException(m.Success ? int.Parse(m.Groups[1].Value) : null, faultString);
            }
            if ((int)resp.StatusCode >= 500 || doc is null)
                throw new SunatNoDisponibleException($"SUNAT/OSE respondió HTTP {(int)resp.StatusCode} sin SOAP Fault.");
            return doc;
        }
    }
}
