using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ABC.Billing.Infrastructure.SunatSoap;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Infraestructura;

public class ClienteSunatTests
{
    private const string CdrManual = """
        <?xml version="1.0" encoding="ISO-8859-1" standalone="no"?>
        <ar:ApplicationResponse xmlns="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:ar="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
        <cbc:ID>201200000230061</cbc:ID>
        <cbc:Note>4031 - Debe indicar el nombre comercial</cbc:Note>
        <cbc:Note>4001 - El numero de RUC del receptor no existe.</cbc:Note>
        <cac:DocumentResponse><cac:Response><cbc:ReferenceID>FA01-981</cbc:ReferenceID><cbc:ResponseCode>0</cbc:ResponseCode><cbc:Description>La Factura numero FA01-981, ha sido aceptada</cbc:Description></cac:Response></cac:DocumentResponse>
        </ar:ApplicationResponse>
        """;

    private static readonly OpcionesSunat Opciones = new("https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService",
        "https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService", Fabrica.RucEmisor, "MODDATOS", "moddatos");

    [Fact]
    public void Lee_codigo_descripcion_y_observaciones_del_CDR_del_manual_de_SUNAT()
    {
        var cdr = LectorCdr.ParsearXml(Encoding.Latin1.GetBytes(CdrManual));
        Assert.Equal(0, cdr.CodigoRespuesta);
        Assert.Contains("aceptada", cdr.Descripcion);
        Assert.Equal(new[] { "4031 - Debe indicar el nombre comercial", "4001 - El numero de RUC del receptor no existe." }, cdr.Observaciones);
    }

    [Fact]
    public void Extrae_el_CDR_del_ZIP_de_respuesta()
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) { using var s = z.CreateEntry("R-20600000013-01-F001-1.xml").Open(); s.Write(Encoding.Latin1.GetBytes(CdrManual)); }
        Assert.Equal(0, LectorCdr.ParsearZip(ms.ToArray()).CodigoRespuesta);
    }

    [Fact]
    public void Un_ZIP_sin_CDR_se_rechaza()
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) { using var s = z.CreateEntry("otro.txt").Open(); s.Write([1, 2, 3]); }
        Assert.Throws<InvalidDataException>(() => LectorCdr.ParsearZip(ms.ToArray()));
    }

    [Fact]
    public void El_ZIP_de_envio_lleva_la_carpeta_dummy_y_el_XML_con_el_mismo_nombre()
    {
        var zip = ClienteSoapSunat.CrearZip("20600000013-01-F001-1", [1, 2, 3]);
        using var z = new ZipArchive(new MemoryStream(zip));
        Assert.Contains(z.Entries, e => e.FullName == "dummy/");
        Assert.Contains(z.Entries, e => e.FullName == "20600000013-01-F001-1.xml");
    }

    [Fact]
    public void El_sobre_SOAP_usa_WS_Security_con_RUC_mas_usuario_SOL()
    {
        var sobre = new ClienteSoapSunat(new HttpClient(), Opciones).ConstruirSobre(new XElement("x")).ToString();
        Assert.Contains($"<wsse:Username>{Fabrica.RucEmisor}MODDATOS</wsse:Username>", sobre);
        Assert.Contains("<wsse:Password>moddatos</wsse:Password>", sobre);
    }
}
