using System.Globalization;
using System.Xml.Linq;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Generacion;

/// <summary>Pruebas unitarias de GENERACIÓN del XML UBL 2.1 a partir del comprobante del dominio.</summary>
public class GeneracionXmlTests
{
    private static readonly Comprobante Completo = Fabrica.ComprobanteCompleto();
    private static readonly byte[] Xml = Fabrica.XmlSinFirma(Completo);
    private static readonly XDocument Doc = Fabrica.Doc(Xml);
    private static XElement Raiz => Doc.Root!;
    private static XNamespace Cbc => Fabrica.Cbc;
    private static XNamespace Cac => Fabrica.Cac;

    private static decimal Num(XElement e) => decimal.Parse(e.Value, CultureInfo.InvariantCulture);
    private static XElement SubtotalCabecera(string codigoTributo) => Raiz.Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal")
        .Single(s => s.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value == codigoTributo);
    private static XElement Linea(int n) => Raiz.Elements(Cac + "InvoiceLine").Single(l => l.Element(Cbc + "ID")!.Value == n.ToString());

    [Fact]
    public void La_declaracion_xml_usa_ISO_8859_1()
        => Assert.StartsWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>", Fabrica.Texto(Xml));

    [Fact]
    public void La_raiz_es_Invoice_en_el_espacio_de_nombres_de_UBL_2_1()
    {
        Assert.Equal("Invoice", Raiz.Name.LocalName);
        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", Raiz.Name.NamespaceName);
    }

    [Fact]
    public void Declara_UBL_2_1_y_la_personalizacion_peruana_2_0()
    {
        Assert.Equal("2.1", Raiz.Element(Cbc + "UBLVersionID")!.Value);
        Assert.Equal("2.0", Raiz.Element(Cbc + "CustomizationID")!.Value);
    }

    [Fact]
    public void La_serie_y_el_correlativo_van_en_cbc_ID()
        => Assert.Equal("F001-1", Raiz.Element(Cbc + "ID")!.Value);

    [Fact]
    public void El_tipo_de_documento_y_el_tipo_de_operacion_usan_codigos_de_catalogo()
    {
        Assert.Equal("01", Raiz.Element(Cbc + "InvoiceTypeCode")!.Value);   // Catálogo 01
        Assert.Equal("0101", Raiz.Element(Cbc + "InvoiceTypeCode")!.Attribute("listID")!.Value);   // Catálogo 51 (SUNAT lo lee de @listID)
        Assert.Null(Raiz.Element(Cbc + "ProfileID"));
    }

    [Fact]
    public void Genera_una_linea_por_cada_item_numerada_desde_1()
    {
        var ids = Raiz.Elements(Cac + "InvoiceLine").Select(l => l.Element(Cbc + "ID")!.Value).ToList();
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6" }, ids);
    }

    [Fact]
    public void La_unidad_de_medida_va_en_unitCode_del_Catalogo_03()
    {
        Assert.Equal("NIU", Linea(1).Element(Cbc + "InvoicedQuantity")!.Attribute("unitCode")!.Value);
        Assert.Equal("KGM", Linea(3).Element(Cbc + "InvoicedQuantity")!.Attribute("unitCode")!.Value);
    }

    [Fact]
    public void Incluye_al_emisor_con_RUC_y_esquema_de_documento_6()
    {
        var emisor = Raiz.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!;
        var ruc = emisor.Element(Cac + "PartyIdentification")!.Element(Cbc + "ID")!;
        Assert.Equal(Fabrica.RucEmisor, ruc.Value);
        Assert.Equal("6", ruc.Attribute("schemeID")!.Value);                 // Catálogo 06
        Assert.Equal("ABC SOFTWARE CORP S.A.C.", emisor.Element(Cac + "PartyLegalEntity")!.Element(Cbc + "RegistrationName")!.Value);
    }

    [Fact]
    public void Incluye_al_cliente_con_su_tipo_de_documento()
    {
        var ruc = Raiz.Element(Cac + "AccountingCustomerParty")!.Descendants(Cac + "PartyIdentification").Single().Element(Cbc + "ID")!;
        Assert.Equal(Fabrica.RucCliente, ruc.Value);
        Assert.Equal("6", ruc.Attribute("schemeID")!.Value);
    }

    [Fact]
    public void Hay_un_solo_TaxTotal_de_cabecera_con_un_subtotal_por_tributo_del_Catalogo_05()
    {
        Assert.Single(Raiz.Elements(Cac + "TaxTotal"));
        var codigos = Raiz.Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal")
            .Select(s => s.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value).ToList();
        Assert.Equal(new[] { "1000", "2000", "9997", "9998", "9996", "7152" }, codigos);
    }

    [Theory]
    [InlineData("1000", "IGV", "VAT", "S")]
    [InlineData("2000", "ISC", "EXC", "S")]
    [InlineData("9997", "EXO", "VAT", "E")]
    [InlineData("9998", "INA", "FRE", "O")]
    [InlineData("9996", "GRA", "FRE", "Z")]
    [InlineData("7152", "ICBPER", "OTH", "S")]
    public void Cada_tributo_se_describe_segun_el_Catalogo_05_y_la_guia_de_SUNAT(string codigo, string nombre, string codigoInternacional, string categoria)
    {
        var sub = SubtotalCabecera(codigo);
        var esquema = sub.Descendants(Cac + "TaxScheme").Single();
        Assert.Equal(nombre, esquema.Element(Cbc + "Name")!.Value);
        Assert.Equal(codigoInternacional, esquema.Element(Cbc + "TaxTypeCode")!.Value);
        Assert.Equal(categoria, sub.Element(Cac + "TaxCategory")!.Element(Cbc + "ID")!.Value);   // UN/ECE 5305
    }

    [Fact]
    public void TierRange_lleva_el_Catalogo_08_solo_en_lineas_con_ISC()
    {
        var tier = Assert.Single(Raiz.Descendants(Cbc + "TierRange"));
        Assert.Equal("01", tier.Value);                   // sistema al valor
        Assert.Empty(tier.Attributes());                   // el XSD no admite atributos de catálogo en TierRange
        Assert.NotNull(Linea(2).Descendants(Cbc + "TierRange").SingleOrDefault());
        Assert.Empty(Linea(1).Descendants(Cbc + "TierRange"));
    }

    [Fact]
    public void En_la_linea_el_ISC_se_calcula_antes_que_el_IGV()
    {
        var tributos = Linea(2).Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal")
            .Select(s => s.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value).ToList();
        Assert.Equal(new[] { "2000", "1000" }, tributos);
        var igv = Linea(2).Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal").Last();
        Assert.Equal("33.00", igv.Element(Cbc + "TaxableAmount")!.Value);    // base IGV = valor de venta + ISC
        Assert.Equal("5.94", igv.Element(Cbc + "TaxAmount")!.Value);
    }

    [Fact]
    public void El_ICBPER_informa_monto_cantidad_de_bolsas_y_tarifa_por_bolsa()
    {
        var sub = Linea(5).Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal")
            .Single(s => s.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value == "7152");
        Assert.Equal("1.50", sub.Element(Cbc + "TaxAmount")!.Value);
        Assert.Equal("3", sub.Element(Cbc + "BaseUnitMeasure")!.Value);
        Assert.Equal("NIU", sub.Element(Cbc + "BaseUnitMeasure")!.Attribute("unitCode")!.Value);
        Assert.Equal("0.50", sub.Descendants(Cbc + "PerUnitAmount").Single().Value);
        Assert.Empty(sub.Elements(Cbc + "TaxableAmount"));    // el ICBPER no tiene base imponible
    }

    [Fact]
    public void Los_totales_de_cabecera_suman_todas_las_operaciones_onerosas()
    {
        var total = Raiz.Element(Cac + "LegalMonetaryTotal")!;
        Assert.Equal("278.30", total.Element(Cbc + "LineExtensionAmount")!.Value);   // gravadas + exoneradas + inafectas
        Assert.Equal("321.19", total.Element(Cbc + "TaxInclusiveAmount")!.Value);
        Assert.Equal("321.19", total.Element(Cbc + "PayableAmount")!.Value);
        Assert.Equal("42.89", Raiz.Element(Cac + "TaxTotal")!.Element(Cbc + "TaxAmount")!.Value);   // IGV + ISC + ICBPER
    }

    [Fact]
    public void Los_importes_del_XML_coinciden_con_los_del_dominio()
    {
        Assert.Equal(Completo.TotalIgv, Num(SubtotalCabecera("1000").Element(Cbc + "TaxAmount")!));
        Assert.Equal(Completo.BaseImponibleIgv, Num(SubtotalCabecera("1000").Element(Cbc + "TaxableAmount")!));
        Assert.Equal(Completo.TotalIsc, Num(SubtotalCabecera("2000").Element(Cbc + "TaxAmount")!));
        Assert.Equal(Completo.TotalIcbper, Num(SubtotalCabecera("7152").Element(Cbc + "TaxAmount")!));
        Assert.Equal(Completo.TotalOperacionesExoneradas, Num(SubtotalCabecera("9997").Element(Cbc + "TaxableAmount")!));
        Assert.Equal(Completo.TotalOperacionesInafectas, Num(SubtotalCabecera("9998").Element(Cbc + "TaxableAmount")!));
        Assert.Equal(Completo.ImporteTotalPagar, Num(Raiz.Descendants(Cbc + "PayableAmount").Single()));
        // En las líneas gratuitas el valor de línea es el valor referencial (SUNAT, error 3271); no entran en el total de la venta
        Assert.Equal(Completo.Lineas.Sum(l => l.AfectacionIgv.EsGratuita() ? l.ValorReferencialTotal : l.ValorVenta),
            Raiz.Elements(Cac + "InvoiceLine").Sum(l => Num(l.Element(Cbc + "LineExtensionAmount")!)));
    }

    [Fact]
    public void Los_importes_se_escriben_con_dos_decimales()
    {
        var importes = Raiz.Descendants().Where(e => e.Name.LocalName.EndsWith("Amount") && e.Attribute("currencyID") != null).ToList();
        Assert.NotEmpty(importes);
        Assert.All(importes, e => Assert.True(e.Value.Contains('.') && e.Value.Split('.')[1].Length >= 2, $"{e.Name.LocalName} = {e.Value}"));
    }

    [Fact]
    public void La_leyenda_1000_lleva_el_monto_en_letras()
    {
        var nota = Raiz.Elements(Cbc + "Note").Single(n => n.Attribute("languageLocaleID")?.Value == "1000");
        Assert.Equal("SON: TRESCIENTOS VEINTIUNO Y 19/100 SOLES", nota.Value);
    }

    [Fact]
    public void La_operacion_gratuita_va_con_precio_cero_y_valor_referencial()
    {
        var l = Linea(6);
        Assert.Equal("20.00", l.Element(Cbc + "LineExtensionAmount")!.Value);   // = valor referencial (SUNAT, error 3271)
        Assert.Equal("0.00", l.Element(Cac + "Price")!.Element(Cbc + "PriceAmount")!.Value);
        var referencia = l.Element(Cac + "PricingReference")!.Element(Cac + "AlternativeConditionPrice")!;
        Assert.Equal("02", referencia.Element(Cbc + "PriceTypeCode")!.Value);   // Catálogo 16: valor referencial
        Assert.Equal("20.00", referencia.Element(Cbc + "PriceAmount")!.Value);
        Assert.Equal("11", l.Descendants(Cbc + "TaxExemptionReasonCode").Single().Value);   // Catálogo 07
    }

    [Fact]
    public void La_operacion_onerosa_usa_precio_con_impuestos_tipo_01()
    {
        var referencia = Linea(1).Element(Cac + "PricingReference")!.Element(Cac + "AlternativeConditionPrice")!;
        Assert.Equal("01", referencia.Element(Cbc + "PriceTypeCode")!.Value);
        Assert.Equal("21.24", referencia.Element(Cbc + "PriceAmount")!.Value);   // 18.00 + 18 % de IGV
        Assert.Equal("18.00", Linea(1).Element(Cac + "Price")!.Element(Cbc + "PriceAmount")!.Value);
    }

    [Fact]
    public void Escapa_ampersand_menor_mayor_y_comillas_sin_perder_el_texto()
    {
        const string razon = "A & B <C> \"D\" 'E'";
        var c = Fabrica.Armar([Fabrica.Linea(1, "Producto", 1m, 10m)], cliente: Fabrica.ClienteConRuc(razon));
        var bytes = Fabrica.XmlSinFirma(c);
        var texto = Fabrica.Texto(bytes);
        Assert.Contains("A &amp; B &lt;C&gt;", texto);
        Assert.DoesNotContain("A & B", texto);
        var registrado = Fabrica.Doc(bytes).Root!.Element(Cac + "AccountingCustomerParty")!.Descendants(Cbc + "RegistrationName").Single().Value;
        Assert.Equal(razon, registrado);
    }

    [Fact]
    public void Conserva_tildes_y_enie_como_bytes_ISO_8859_1()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Ñandú café con piña", 1m, 10m)]);
        var bytes = Fabrica.XmlSinFirma(c);
        Assert.True(Array.IndexOf(bytes, (byte)0xD1) >= 0, "la Ñ debe ser el byte 0xD1 de ISO-8859-1");
        Assert.Contains("Ñandú café con piña", Fabrica.Texto(bytes));
    }

    [Fact]
    public void Un_caracter_fuera_de_ISO_8859_1_no_rompe_el_XML()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Oferta 😀 especial", 1m, 10m)]);
        var descripcion = Fabrica.Doc(Fabrica.XmlSinFirma(c)).Root!.Descendants(Cac + "Item").Single().Element(Cbc + "Description")!.Value;
        Assert.Equal("Oferta 😀 especial", descripcion);   // se escribe como referencia numérica y se lee igual
    }

    [Fact]
    public void Una_exportacion_usa_el_tributo_9995_y_el_tipo_de_operacion_0102()
    {
        var cliente = new Cliente { TipoDocumento = TipoDocumentoIdentidad.NoDomiciliadoSinRuc, NumeroDocumento = "-", RazonSocial = "IMPORTER LLC" };
        var c = Fabrica.Armar([Fabrica.Linea(1, "Quinua para exportación", 100m, 8m, AfectacionIgv.Exportacion, UnidadMedida.KGM)],
            operacion: TipoOperacion.Exportacion, cliente: cliente);
        var raiz = Fabrica.Doc(Fabrica.XmlSinFirma(c)).Root!;
        Assert.Equal("0102", raiz.Element(Cbc + "InvoiceTypeCode")!.Attribute("listID")!.Value);
        var sub = raiz.Element(Cac + "TaxTotal")!.Element(Cac + "TaxSubtotal")!;
        Assert.Equal("9995", sub.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value);
        Assert.Equal("G", sub.Element(Cac + "TaxCategory")!.Element(Cbc + "ID")!.Value);
        Assert.Equal("800.00", raiz.Element(Cac + "LegalMonetaryTotal")!.Element(Cbc + "PayableAmount")!.Value);
    }

    [Fact]
    public void Una_boleta_usa_el_tipo_03_y_serie_B()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Arroz", 2m, 18m)], TipoComprobante.BoletaDeVenta, serie: "B001");
        var raiz = Fabrica.Doc(Fabrica.XmlSinFirma(c)).Root!;
        Assert.Equal("03", raiz.Element(Cbc + "InvoiceTypeCode")!.Value);
        Assert.Equal("B001-1", raiz.Element(Cbc + "ID")!.Value);
    }

    [Fact]
    public void Si_todo_es_gratuito_agrega_la_leyenda_1002()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Obsequio", 1m, 10m, AfectacionIgv.GravadoRetiroPorPremio)]);
        var notas = Fabrica.Doc(Fabrica.XmlSinFirma(c)).Root!.Elements(Cbc + "Note").Select(n => n.Attribute("languageLocaleID")!.Value).ToList();
        Assert.Contains("1002", notas);
        Assert.DoesNotContain("1002", Raiz.Elements(Cbc + "Note").Select(n => n.Attribute("languageLocaleID")!.Value));
    }


    [Fact]
    public void LineCountNumeric_informa_la_cantidad_de_items_como_en_los_ejemplos_de_la_guia()
        => Assert.Equal("6", Raiz.Element(Cbc + "LineCountNumeric")!.Value);

    [Fact]
    public void Los_textos_de_los_atributos_de_catalogo_son_los_que_validan_las_reglas_de_SUNAT()
    {
        // Valores tomados de las reglas de validación ValidaExprRegFactura-2.0.1.xsl (errores/observaciones 4252 a 4257)
        var tipoDoc = Raiz.Element(Cbc + "InvoiceTypeCode")!;
        Assert.Equal("Tipo de Documento", tipoDoc.Attribute("listName")!.Value);
        Assert.Equal("PE:SUNAT", tipoDoc.Attribute("listAgencyName")!.Value);
        Assert.Equal("Tipo de Operacion", tipoDoc.Attribute("name")!.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo01", tipoDoc.Attribute("listURI")!.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo51", tipoDoc.Attribute("listSchemeURI")!.Value);
        Assert.Equal("ISO 4217 Alpha", Raiz.Element(Cbc + "DocumentCurrencyCode")!.Attribute("listID")!.Value);

        var ruc = Raiz.Element(Cac + "AccountingSupplierParty")!.Descendants(Cac + "PartyIdentification").Single().Element(Cbc + "ID")!;
        Assert.Equal("Documento de Identidad", ruc.Attribute("schemeName")!.Value);
        Assert.Equal("PE:SUNAT", ruc.Attribute("schemeAgencyName")!.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo06", ruc.Attribute("schemeURI")!.Value);

        var tipoPrecio = Linea(1).Descendants(Cbc + "PriceTypeCode").Single();
        Assert.Equal("Tipo de Precio", tipoPrecio.Attribute("listName")!.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo16", tipoPrecio.Attribute("listURI")!.Value);

        var afectacion = Linea(1).Descendants(Cbc + "TaxExemptionReasonCode").Single();
        Assert.Equal("Afectacion del IGV", afectacion.Attribute("listName")!.Value);
        Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo07", afectacion.Attribute("listURI")!.Value);
    }

    [Fact]
    public void El_codigo_de_establecimiento_del_emisor_va_en_PartyLegalEntity_con_su_lista()
    {
        var codigo = Raiz.Element(Cac + "AccountingSupplierParty")!.Descendants(Cbc + "AddressTypeCode").Single();
        Assert.Equal("0000", codigo.Value);
        Assert.Equal("Establecimientos anexos", codigo.Attribute("listName")!.Value);
        Assert.Equal("PE:SUNAT", codigo.Attribute("listAgencyName")!.Value);
        Assert.Equal("PartyLegalEntity", codigo.Parent!.Parent!.Name.LocalName);
    }

    [Fact]
    public void El_TaxScheme_de_cabecera_y_el_de_cada_linea_llevan_los_mismos_atributos_de_catalogo_05()
    {
        var cabecera = SubtotalCabecera("1000").Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!;
        var linea = Linea(1).Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!;
        foreach (var id in new[] { cabecera, linea })
        {
            Assert.Equal("Codigo de tributos", id.Attribute("schemeName")!.Value);
            Assert.Equal("PE:SUNAT", id.Attribute("schemeAgencyName")!.Value);
            Assert.Equal("urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo05", id.Attribute("schemeURI")!.Value);
        }
    }

    [Fact]
    public void La_forma_de_pago_es_contado_como_exigen_las_reglas_de_SUNAT()
    {
        var pago = Raiz.Element(Cac + "PaymentTerms")!;
        Assert.Equal("FormaPago", pago.Element(Cbc + "ID")!.Value);
        Assert.Equal("Contado", pago.Element(Cbc + "PaymentMeansID")!.Value);
    }

    [Fact]
    public void El_total_de_impuestos_de_cada_linea_es_la_suma_de_sus_tributos_sin_contar_el_9996_de_las_gratuitas()
    {
        // Regla 3292 de SUNAT/OSE: TaxTotal/TaxAmount de la línea = suma de los subtotales 1000, 1016, 2000, 7152 y 9999.
        foreach (var linea in Raiz.Elements(Cac + "InvoiceLine"))
        {
            var total = decimal.Parse(linea.Element(Cac + "TaxTotal")!.Element(Cbc + "TaxAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            var suma = linea.Descendants(Cac + "TaxSubtotal")
                .Where(s => s.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value is "1000" or "1016" or "2000" or "7152" or "9999")
                .Sum(s => decimal.Parse(s.Element(Cbc + "TaxAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(suma, total);
        }
        // La línea gratuita (11) informa su IGV de 3.60 en el subtotal 9996 (regla 3111), pero el total de la línea es 0.00.
        var gratuita = Raiz.Elements(Cac + "InvoiceLine").Last();
        Assert.Equal("0.00", gratuita.Element(Cac + "TaxTotal")!.Element(Cbc + "TaxAmount")!.Value);
        Assert.Equal("3.60", gratuita.Descendants(Cac + "TaxSubtotal").Single().Element(Cbc + "TaxAmount")!.Value);
    }

    [Fact]
    public void Todo_subtotal_de_linea_lleva_Percent_salvo_ICBPER_y_ISC_de_monto_fijo()
    {
        foreach (var sub in Raiz.Elements(Cac + "InvoiceLine").SelectMany(l => l.Descendants(Cac + "TaxSubtotal")))
        {
            var codigo = sub.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value;
            var porcentaje = sub.Element(Cac + "TaxCategory")!.Element(Cbc + "Percent");
            if (codigo == "7152") Assert.Null(porcentaje);
            else Assert.NotNull(porcentaje);
        }
        // exonerado e inafecto: 0.00; gravado: 18.00
        Assert.Equal("0.00", Linea(3).Descendants(Cbc + "Percent").Single().Value);
        Assert.Equal("0.00", Linea(4).Descendants(Cbc + "Percent").Single().Value);
    }

    [Fact]
    public void La_generacion_es_determinista()
        => Assert.Equal(Xml, Fabrica.XmlSinFirma(Fabrica.ComprobanteCompleto()));

    [Fact]
    public void El_documento_sin_firmar_deja_vacio_el_ExtensionContent_donde_ira_la_firma()
    {
        var extension = Raiz.Element(Fabrica.Ext + "UBLExtensions")!.Elements(Fabrica.Ext + "UBLExtension").Single();
        Assert.Empty(extension.Element(Fabrica.Ext + "ExtensionContent")!.Elements());
    }
}
