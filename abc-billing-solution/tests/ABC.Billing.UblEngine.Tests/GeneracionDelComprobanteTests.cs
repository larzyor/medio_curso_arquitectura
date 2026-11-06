using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Infrastructure.UblEngine.Dominio;
using ABC.Billing.UblEngine.Tests.Apoyo;

namespace ABC.Billing.UblEngine.Tests;

/// <summary>Encabezado, participantes y totales del comprobante en el XML UBL 2.1 generado.</summary>
public class GeneracionDelComprobanteTests
{
    private static XmlUbl FacturaDeArroz() => UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 10m, 18.00m).Xml();

    [Fact]
    public void El_documento_es_un_Invoice_UBL_21_con_la_personalizacion_peruana_20()
    {
        var xml = FacturaDeArroz();

        Assert.Equal(XmlUbl.Inv + "Invoice", xml.Raiz.Name);
        Assert.Equal("2.1", xml.Valor("cbc:UBLVersionID"));
        Assert.Equal("2.0", xml.Valor("cbc:CustomizationID"));
    }

    [Fact]
    public void Los_elementos_del_encabezado_salen_en_el_orden_que_exige_el_xsd()
    {
        var orden = FacturaDeArroz().Raiz.Elements().Select(e => e.Name.LocalName).Distinct();

        Assert.Equal(new[]
        {
            "UBLExtensions", "UBLVersionID", "CustomizationID", "ProfileID", "ID", "IssueDate", "IssueTime",
            "InvoiceTypeCode", "Note", "DocumentCurrencyCode", "Signature", "AccountingSupplierParty",
            "AccountingCustomerParty", "TaxTotal", "LegalMonetaryTotal", "InvoiceLine"
        }, orden);
    }

    [Fact]
    public void La_serie_numero_fecha_y_hora_de_emision_identifican_el_comprobante()
    {
        var xml = FacturaDeArroz();

        Assert.Equal("F001-1", xml.Valor("cbc:ID"));
        Assert.Equal("2026-11-03", xml.Valor("cbc:IssueDate"));
        Assert.Equal("10:15:00", xml.Valor("cbc:IssueTime"));
    }

    [Theory]
    [InlineData(TipoComprobante.Factura, "01")]
    [InlineData(TipoComprobante.BoletaDeVenta, "03")]
    public void El_tipo_de_comprobante_usa_el_catalogo_01(TipoComprobante tipo, string codigo)
    {
        var comprobante = tipo == TipoComprobante.Factura ? UnComprobante.Factura() : UnComprobante.Boleta();
        var tipoDoc = comprobante.ConLinea("Arroz extra 5 kg", 1m, 18.00m).Xml().Uno("cbc:InvoiceTypeCode");

        Assert.Equal(codigo, tipoDoc.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo01", (string?)tipoDoc.Attribute("listURI"));
        Assert.Equal("PE:SUNAT", (string?)tipoDoc.Attribute("listAgencyName"));
    }

    [Fact]
    public void El_tipo_de_operacion_va_en_ProfileID_con_el_catalogo_51()
    {
        var operacion = FacturaDeArroz().Uno("cbc:ProfileID");

        Assert.Equal("0101", operacion.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo51", (string?)operacion.Attribute("schemeURI"));
    }

    [Fact]
    public void La_leyenda_1000_lleva_el_importe_total_en_letras()
    {
        var leyenda = FacturaDeArroz().Uno("cbc:Note");

        Assert.Equal("1000", (string?)leyenda.Attribute("languageLocaleID"));
        Assert.Equal("SON: DOSCIENTOS DOCE Y 40/100 SOLES", leyenda.Value); // 180.00 + 18 % = 212.40
    }

    [Fact]
    public void Un_comprobante_solo_con_entregas_gratuitas_agrega_la_leyenda_1002()
    {
        var gratuito = UnComprobante.Factura().ConLinea("Muestra gratuita", 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio).Xml();
        var oneroso = FacturaDeArroz();

        Assert.Contains("1002", gratuito.Todos("cbc:Note").Select(n => (string?)n.Attribute("languageLocaleID")));
        Assert.DoesNotContain("1002", oneroso.Todos("cbc:Note").Select(n => (string?)n.Attribute("languageLocaleID")));
    }

    [Fact]
    public void La_moneda_del_comprobante_se_aplica_a_todos_los_importes()
    {
        var xml = UnComprobante.Factura().EnDolares().ConLinea("Arroz extra 5 kg", 10m, 18.00m).Xml();

        Assert.Equal("USD", xml.Valor("cbc:DocumentCurrencyCode"));
        var monedas = xml.Raiz.Descendants().Where(e => e.Name.LocalName.EndsWith("Amount")).Select(e => (string?)e.Attribute("currencyID")).Distinct();
        Assert.Equal(new[] { "USD" }, monedas);
    }

    [Fact]
    public void El_emisor_se_identifica_con_su_RUC_razon_social_nombre_comercial_y_establecimiento()
    {
        var xml = FacturaDeArroz();
        var emisor = xml.Uno("cac:AccountingSupplierParty/cac:Party");

        Assert.Equal("ABC Abarrotes", xml.Valor("cac:PartyName/cbc:Name", emisor));
        Assert.Equal("ABC SOFTWARE CORP S.A.C.", xml.Valor("cac:PartyTaxScheme/cbc:RegistrationName", emisor));
        var ruc = xml.Uno("cac:PartyTaxScheme/cbc:CompanyID", emisor);
        Assert.Equal(UnComprobante.RucEmisor, ruc.Value);
        Assert.Equal("6", (string?)ruc.Attribute("schemeID"));
        Assert.Equal("0000", xml.Valor("cac:PartyTaxScheme/cac:RegistrationAddress/cbc:AddressTypeCode", emisor));
    }

    [Fact]
    public void El_emisor_firma_y_la_firma_apunta_a_las_extensiones()
    {
        var xml = FacturaDeArroz();
        var firma = xml.Uno("cac:Signature");

        Assert.Equal("SignABC", xml.Valor("cbc:ID", firma));
        Assert.Equal(UnComprobante.RucEmisor, xml.Valor("cac:SignatoryParty/cac:PartyIdentification/cbc:ID", firma));
        Assert.Equal("#SignABC", xml.Valor("cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI", firma));
        Assert.Empty(xml.Uno("ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent").Elements()); // la llena el firmador
    }

    [Theory]
    [InlineData(true, UnComprobante.RucCliente, "6")]
    [InlineData(false, "45678912", "1")]
    public void El_cliente_se_identifica_con_su_documento_del_catalogo_06(bool esFactura, string numero, string tipoDocumento)
    {
        var comprobante = esFactura ? UnComprobante.Factura() : UnComprobante.Boleta();
        var xml = comprobante.ConLinea("Arroz extra 5 kg", 1m, 18.00m).Xml();
        var documento = xml.Uno("cac:AccountingCustomerParty/cac:Party/cac:PartyTaxScheme/cbc:CompanyID");

        Assert.Equal(numero, documento.Value);
        Assert.Equal(tipoDocumento, (string?)documento.Attribute("schemeID"));
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06", (string?)documento.Attribute("schemeURI"));
    }

    [Fact]
    public void Los_totales_son_valor_de_venta_precio_de_venta_e_importe_a_pagar()
    {
        var xml = FacturaDeArroz();

        Assert.Equal("180.00", xml.Valor("cac:LegalMonetaryTotal/cbc:LineExtensionAmount"));
        Assert.Equal("212.40", xml.Valor("cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount"));
        Assert.Equal("212.40", xml.Valor("cac:LegalMonetaryTotal/cbc:PayableAmount"));
    }

    [Fact]
    public void Hay_una_InvoiceLine_por_cada_linea_numerada_desde_1()
    {
        var xml = UnComprobante.Factura()
            .ConLinea("Arroz extra 5 kg", 10m, 18.00m)
            .ConLinea("Quinua 1 kg", 5m, 12.00m, AfectacionIgv.ExoneradoOperacionOnerosa, UnidadMedida.KGM)
            .ConLinea("Sal de mesa", 2m, 4.00m, AfectacionIgv.InafectoOperacionOnerosa)
            .Xml();

        Assert.Equal(new[] { "1", "2", "3" }, xml.Lineas.Select(l => xml.Valor("cbc:ID", l)));
        Assert.Equal("Quinua 1 kg", xml.Valor("cac:Item/cbc:Description", xml.Lineas[1]));
        Assert.Equal("KGM", (string?)xml.Uno("cbc:InvoicedQuantity", xml.Lineas[1]).Attribute("unitCode"));
    }

    [Fact]
    public void El_vocabulario_del_dominio_tambien_sirve_sin_pasar_por_la_entidad()
    {
        // ComprobanteUbl es la "frase" en lenguaje del dominio; aquí se arma solo el encabezado y una leyenda.
        var documento = ComprobanteUbl.Nuevo(TipoComprobante.Factura, "F002-15", UnComprobante.FechaEmision, TipoMoneda.PEN)
            .DeTipoDeOperacion(TipoOperacion.Exportacion)
            .ConLeyenda(Leyenda.MontoEnLetras, "SON: CIEN Y 00/100 SOLES")
            .EmitidoPor(UnComprobante.Emisor)
            .Documento;

        Assert.Equal("F002-15", documento.ID!.Value);
        Assert.Equal("0102", documento.ProfileID!.Value);
        Assert.Equal("1000", documento.Note.Single().LanguageLocaleID);
        Assert.Equal(UnComprobante.RucEmisor, documento.AccountingSupplierParty!.Party!.PartyTaxScheme.Single().CompanyID!.Value);
    }
}
