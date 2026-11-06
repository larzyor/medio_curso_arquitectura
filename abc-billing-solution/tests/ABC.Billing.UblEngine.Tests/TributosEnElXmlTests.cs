using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.UblEngine.Tests.Apoyo;

namespace ABC.Billing.UblEngine.Tests;

/// <summary>Tributos del Catálogo 05 en el XML: por línea (ISC → IGV → ICBPER) y en el resumen del comprobante.</summary>
public class TributosEnElXmlTests
{
    [Theory]
    [InlineData(AfectacionIgv.GravadoOperacionOnerosa, "1000", "IGV", "S", "VAT")]
    [InlineData(AfectacionIgv.ExoneradoOperacionOnerosa, "9997", "EXO", "E", "VAT")]
    [InlineData(AfectacionIgv.InafectoOperacionOnerosa, "9998", "INA", "O", "FRE")]
    [InlineData(AfectacionIgv.Exportacion, "9995", "EXP", "G", "FRE")]
    [InlineData(AfectacionIgv.GravadoRetiroPorPremio, "9996", "GRA", "Z", "FRE")]
    public void La_afectacion_al_IGV_decide_el_tributo_de_la_linea_y_del_resumen(
        AfectacionIgv afectacion, string codigoTributo, string nombre, string categoriaUnece, string codigoInternacional)
    {
        var xml = UnComprobante.Factura().ConLinea("Producto", 2m, 50.00m, afectacion).Xml();

        var deLaLinea = Assert.Single(xml.Todos("cac:TaxTotal/cac:TaxSubtotal", xml.Lineas[0]));
        var delResumen = Assert.Single(xml.Todos("cac:TaxTotal/cac:TaxSubtotal"));
        foreach (var subtotal in new[] { deLaLinea, delResumen })
        {
            Assert.Equal(codigoTributo, xml.Valor("cac:TaxCategory/cac:TaxScheme/cbc:ID", subtotal));
            Assert.Equal(nombre, xml.Valor("cac:TaxCategory/cac:TaxScheme/cbc:Name", subtotal));
            Assert.Equal(categoriaUnece, xml.Valor("cac:TaxCategory/cbc:ID", subtotal));
            Assert.Equal(codigoInternacional, xml.Valor("cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode", subtotal));
            Assert.Equal("100.00", xml.Valor("cbc:TaxableAmount", subtotal));
        }
        // La afectación (Catálogo 07) se informa en la línea.
        Assert.Equal(afectacion.Codigo(), xml.Valor("cac:TaxCategory/cbc:TaxExemptionReasonCode", deLaLinea));
    }

    [Fact]
    public void Una_linea_gravada_informa_IGV_del_18_por_ciento_y_el_precio_con_impuestos()
    {
        var xml = UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 10m, 18.00m, codigo: "ARR-5KG").Xml();
        var linea = xml.Lineas[0];

        Assert.Equal("10.00", xml.Valor("cbc:InvoicedQuantity", linea));
        Assert.Equal("NIU", (string?)xml.Uno("cbc:InvoicedQuantity", linea).Attribute("unitCode"));
        Assert.Equal("180.00", xml.Valor("cbc:LineExtensionAmount", linea));
        Assert.Equal("32.40", xml.Valor("cac:TaxTotal/cbc:TaxAmount", linea));
        Assert.Equal("18.00", xml.Valor("cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cbc:Percent", linea));
        // Precio de venta unitario con IGV (Catálogo 16 = 01) y valor unitario sin impuestos.
        Assert.Equal("21.24", xml.Valor("cac:PricingReference/cac:AlternativeConditionPrice/cbc:PriceAmount", linea));
        Assert.Equal("01", xml.Valor("cac:PricingReference/cac:AlternativeConditionPrice/cbc:PriceTypeCode", linea));
        Assert.Equal("18.00", xml.Valor("cac:Price/cbc:PriceAmount", linea));
        Assert.Equal("ARR-5KG", xml.Valor("cac:Item/cac:SellersItemIdentification/cbc:ID", linea));
    }

    [Fact]
    public void El_ISC_al_valor_va_antes_del_IGV_y_entra_en_su_base()
    {
        var xml = UnComprobante.Factura().ConLineaAfectaAlIscAlValor("Cerveza 620 ml", 6m, 5.00m, tasaIsc: 10m).Xml();
        var subtotales = xml.Todos("cac:TaxTotal/cac:TaxSubtotal", xml.Lineas[0]).ToList();

        Assert.Equal(new[] { "2000", "1000" }, subtotales.Select(s => xml.Valor("cac:TaxCategory/cac:TaxScheme/cbc:ID", s)));
        var isc = subtotales[0];
        Assert.Equal("30.00", xml.Valor("cbc:TaxableAmount", isc));
        Assert.Equal("3.00", xml.Valor("cbc:TaxAmount", isc));
        Assert.Equal("10.00", xml.Valor("cac:TaxCategory/cbc:Percent", isc));
        Assert.Equal("01", xml.Valor("cac:TaxCategory/cbc:TierRange", isc));          // Catálogo 08: sistema al valor
        Assert.Empty(xml.Uno("cac:TaxCategory/cbc:TierRange", isc).Attributes());     // el XSD no admite atributos aquí
        var igv = subtotales[1];
        Assert.Equal("33.00", xml.Valor("cbc:TaxableAmount", igv));                   // 30.00 + ISC 3.00
        Assert.Equal("5.94", xml.Valor("cbc:TaxAmount", igv));
        Assert.Equal("8.94", xml.Valor("cac:TaxTotal/cbc:TaxAmount", xml.Lineas[0]));
    }

    [Fact]
    public void El_ICBPER_se_cobra_por_bolsa_sin_base_imponible()
    {
        var xml = UnComprobante.Factura().ConBolsasPlasticas(3).Xml();
        var icbper = xml.SubtotalesPorTributo(xml.Uno("cac:TaxTotal", xml.Lineas[0]))["7152"];

        Assert.Empty(xml.Todos("cbc:TaxableAmount", icbper));
        Assert.Equal("1.50", xml.Valor("cbc:TaxAmount", icbper));
        Assert.Equal("3", xml.Valor("cbc:BaseUnitMeasure", icbper));
        Assert.Equal("NIU", (string?)xml.Uno("cbc:BaseUnitMeasure", icbper).Attribute("unitCode"));
        Assert.Equal("0.50", xml.Valor("cac:TaxCategory/cbc:PerUnitAmount", icbper));
        // IGV de las bolsas (0.30 × 18 % = 0.05) + ICBPER 1.50
        Assert.Equal("1.55", xml.Valor("cac:TaxTotal/cbc:TaxAmount", xml.Lineas[0]));
    }

    [Fact]
    public void Una_entrega_gratuita_usa_valor_referencial_y_no_suma_al_importe_a_pagar()
    {
        var xml = UnComprobante.Factura()
            .ConLinea("Arroz extra 5 kg", 1m, 18.00m)
            .ConLinea("Muestra gratuita", 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio)
            .Xml();
        var gratuita = xml.Lineas[1];

        Assert.Equal("02", xml.Valor("cac:PricingReference/cac:AlternativeConditionPrice/cbc:PriceTypeCode", gratuita));
        Assert.Equal("20.00", xml.Valor("cac:PricingReference/cac:AlternativeConditionPrice/cbc:PriceAmount", gratuita));
        Assert.Equal("0.00", xml.Valor("cac:Price/cbc:PriceAmount", gratuita));
        Assert.Equal("3.60", xml.Valor("cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount", gratuita)); // IGV informativo
        Assert.Equal("21.24", xml.Valor("cac:LegalMonetaryTotal/cbc:PayableAmount"));             // solo el arroz
    }

    [Fact]
    public void El_resumen_es_un_solo_TaxTotal_con_un_subtotal_por_tributo_y_el_total_de_tributos()
    {
        var xml = UnComprobante.Factura()
            .ConLinea("Arroz extra 5 kg", 10m, 18.00m)
            .ConLineaAfectaAlIscAlValor("Cerveza 620 ml", 6m, 5.00m, tasaIsc: 10m)
            .ConLinea("Quinua 1 kg", 5m, 12.00m, AfectacionIgv.ExoneradoOperacionOnerosa)
            .ConLinea("Sal de mesa", 2m, 4.00m, AfectacionIgv.InafectoOperacionOnerosa)
            .ConBolsasPlasticas(3)
            .ConLinea("Muestra gratuita", 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio)
            .Xml();

        var resumen = xml.Uno("cac:TaxTotal");
        var porTributo = xml.SubtotalesPorTributo(resumen);
        Assert.Equal(new[] { "1000", "2000", "9997", "9998", "9996", "7152" }, porTributo.Keys);
        Assert.Equal("42.89", xml.Valor("cbc:TaxAmount", resumen));                // IGV 38.39 + ISC 3.00 + ICBPER 1.50
        Assert.Equal("213.30", xml.Valor("cbc:TaxableAmount", porTributo["1000"])); // gravadas 210.30 + ISC 3.00
        Assert.Equal("38.39", xml.Valor("cbc:TaxAmount", porTributo["1000"]));
        Assert.Equal("278.30", xml.Valor("cac:LegalMonetaryTotal/cbc:LineExtensionAmount"));
        Assert.Equal("321.19", xml.Valor("cac:LegalMonetaryTotal/cbc:PayableAmount"));
    }
}
