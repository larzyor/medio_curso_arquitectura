using ABC.Billing.Core.Domain;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Generacion;

/// <summary>El XML generado se valida contra el XSD OFICIAL de OASIS (UBL-Invoice-2.1.xsd).</summary>
public class ValidacionXsdTests
{
    private static readonly byte[] Firmado = Fabrica.XmlFirmado(Fabrica.ComprobanteCompleto());
    private static string Texto => Fabrica.Texto(Firmado);
    private static byte[] Bytes(string xml) => System.Text.Encoding.Latin1.GetBytes(xml);

    [Fact]
    public void La_factura_completa_firmada_cumple_el_XSD() => Fabrica.Validador.Validar(Firmado);

    [Fact]
    public void Una_boleta_firmada_cumple_el_XSD()
        => Fabrica.Validador.Validar(Fabrica.XmlFirmado(Fabrica.Armar([Fabrica.Linea(1, "Arroz", 2m, 18m)],
            ABC.Billing.Core.Domain.Enums.TipoComprobante.BoletaDeVenta, serie: "B001")));

    [Fact]
    public void Sin_firma_el_documento_no_cumple_porque_el_XSD_exige_contenido_en_ExtensionContent()
    {
        var error = Assert.Throws<UblValidationException>(() => Fabrica.Validador.Validar(Fabrica.XmlSinFirma(Fabrica.ComprobanteCompleto())));
        Assert.NotEmpty(error.Errores);
    }

    [Fact]
    public void Un_elemento_desconocido_se_rechaza()
        => Assert.Throws<UblValidationException>(() => Fabrica.Validador.Validar(
            Bytes(Texto.Replace("<cac:LegalMonetaryTotal>", "<cac:LegalMonetaryTotalX>").Replace("</cac:LegalMonetaryTotal>", "</cac:LegalMonetaryTotalX>"))));

    [Fact]
    public void Falta_de_un_elemento_obligatorio_se_rechaza()
    {
        var sinImporteAPagar = System.Text.RegularExpressions.Regex.Replace(Texto, @"<cbc:PayableAmount[^>]*>[^<]*</cbc:PayableAmount>", "");
        var error = Assert.Throws<UblValidationException>(() => Fabrica.Validador.Validar(Bytes(sinImporteAPagar)));
        Assert.Contains(error.Errores, e => e.Contains("PayableAmount"));
    }

    [Fact]
    public void Un_atributo_no_permitido_se_rechaza()
        => Assert.Throws<UblValidationException>(() => Fabrica.Validador.Validar(Bytes(Texto.Replace("<cbc:TierRange>", "<cbc:TierRange listName=\"x\">"))));

    [Fact]
    public void Una_fecha_de_emision_imposible_se_rechaza()
    {
        var mala = System.Text.RegularExpressions.Regex.Replace(Texto, @"<cbc:IssueDate>[^<]*</cbc:IssueDate>", "<cbc:IssueDate>2026-13-45</cbc:IssueDate>");
        Assert.Throws<UblValidationException>(() => Fabrica.Validador.Validar(Bytes(mala)));
    }

    [Fact]
    public void Un_importe_no_numerico_se_rechaza()
    {
        var mala = System.Text.RegularExpressions.Regex.Replace(Texto, @"(<cbc:PayableAmount[^>]*>)[^<]*(</cbc:PayableAmount>)", "$1abc$2");
        Assert.Throws<UblValidationException>(() => Fabrica.Validador.Validar(Bytes(mala)));
    }

    [Fact]
    public void Cada_comprobante_de_los_distintos_escenarios_genera_XML_valido()
    {
        var escenarios = new[]
        {
            Fabrica.Armar([Fabrica.Linea(1, "Solo gravado", 3m, 10m)]),
            Fabrica.Armar([Fabrica.Linea(1, "Solo exonerado", 3m, 10m, ABC.Billing.Core.Domain.Enums.AfectacionIgv.ExoneradoOperacionOnerosa)]),
            Fabrica.Armar([Fabrica.Linea(1, "ISC monto fijo", 4m, 10m, isc: ABC.Billing.Core.Domain.Enums.SistemaIsc.MontoFijo, montoFijoIsc: 1.5m)]),
            Fabrica.Armar([Fabrica.Linea(1, "Gratuito inafecto", 1m, 10m, ABC.Billing.Core.Domain.Enums.AfectacionIgv.InafectoRetiroPorMuestrasMedicas)]),
            Fabrica.Armar([Fabrica.Linea(1, "Bolsas", 5m, 0.10m, icbper: true)])
        };
        foreach (var c in escenarios) Fabrica.Validador.Validar(Fabrica.XmlFirmado(c));
    }
}
