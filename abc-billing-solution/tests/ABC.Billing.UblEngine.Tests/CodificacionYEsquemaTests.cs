using System.Globalization;
using System.Text;
using System.Xml.Serialization;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Infrastructure.UblEngine.Generated;
using ABC.Billing.Infrastructure.UblEngine.Serializers;
using ABC.Billing.UblEngine.Tests.Apoyo;

namespace ABC.Billing.UblEngine.Tests;

/// <summary>Codificación ISO-8859-1, escape de caracteres, formato numérico y validez contra el XSD oficial.</summary>
public class CodificacionYEsquemaTests
{
    private static readonly UblXsdValidator Validador = new();
    private static readonly XmlDsigCertificateSigner Firmador = new(CertificadoDePrueba.Crear(UnComprobante.RucEmisor));

    public static IEnumerable<object[]> ComprobantesVariados() =>
    [
        ["factura gravada", UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 10m, 18.00m)],
        ["boleta a DNI", UnComprobante.Boleta().ConLinea("Arroz extra 5 kg", 2m, 18.00m)],
        ["factura en dólares", UnComprobante.Factura().EnDolares().ConLinea("Servicio de soporte", 1m, 250.00m)],
        ["ISC + ICBPER", UnComprobante.Factura().ConLineaAfectaAlIscAlValor("Cerveza 620 ml", 6m, 5.00m, 10m).ConBolsasPlasticas(3)],
        ["exonerado, inafecto y exportación", UnComprobante.Factura()
            .ConLinea("Quinua 1 kg", 5m, 12.00m, AfectacionIgv.ExoneradoOperacionOnerosa, UnidadMedida.KGM)
            .ConLinea("Sal de mesa", 2m, 4.00m, AfectacionIgv.InafectoOperacionOnerosa)
            .ConLinea("Café para exportar", 3m, 40.00m, AfectacionIgv.Exportacion)],
        ["solo gratuitas", UnComprobante.Factura().ConLinea("Muestra gratuita", 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio)]
    ];

    [Theory]
    [MemberData(nameof(ComprobantesVariados))]
    public void El_comprobante_firmado_cumple_UBL_Invoice_21_xsd(string caso, UnComprobante comprobante)
    {
        var firmado = Firmador.Firmar(comprobante.XmlGenerado()).Xml;

        var error = Record.Exception(() => Validador.Validar(firmado));

        Assert.True(error is null, $"{caso}: {error?.Message}");
    }

    [Fact]
    public void Sin_firma_el_xsd_lo_rechaza_porque_ExtensionContent_queda_vacio()
    {
        // Por eso el caso de uso valida el documento YA firmado (especificación, 10.5).
        var sinFirmar = UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 1m, 18.00m).XmlGenerado();

        var error = Assert.Throws<UblValidationException>(() => Validador.Validar(sinFirmar));
        Assert.Contains(error.Errores, e => e.Contains("ExtensionContent"));
    }

    [Fact]
    public void El_xsd_rechaza_un_elemento_que_no_existe_en_UBL()
    {
        var texto = Encoding.Latin1.GetString(Firmador.Firmar(UnComprobante.Factura().ConLinea("Arroz", 1m, 18.00m).XmlGenerado()).Xml);
        var alterado = Encoding.Latin1.GetBytes(texto.Replace("cac:LegalMonetaryTotal>", "cac:TotalesLegales>"));

        Assert.Throws<UblValidationException>(() => Validador.Validar(alterado));
    }

    [Fact]
    public void La_declaracion_es_ISO_8859_1_como_pide_el_Manual_del_Programador()
    {
        var xml = UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 1m, 18.00m).Xml();

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>", xml.Texto);
    }

    [Fact]
    public void La_enie_y_las_tildes_se_escriben_como_un_byte_ISO_8859_1()
    {
        var bytes = UnComprobante.Factura().ParaElCliente("BODEGA EL ÑANDÚ S.A.C.").ConLinea("Azúcar rubia", 1m, 4.00m).XmlGenerado();

        Assert.Contains((byte)0xD1, bytes);   // Ñ
        Assert.Contains((byte)0xDA, bytes);   // Ú
        Assert.DoesNotContain("Ã", Encoding.Latin1.GetString(bytes)); // no hay UTF-8 mal codificado
        Assert.Contains("BODEGA EL ÑANDÚ S.A.C.", XmlUbl.Leer(bytes).Valor("cac:AccountingCustomerParty/cac:Party/cac:PartyTaxScheme/cbc:RegistrationName"));
    }

    [Fact]
    public void Los_caracteres_especiales_los_escapa_el_serializador()
    {
        const string razonSocial = "BODEGA \"EL ÑANDÚ\" & HIJOS S.A.C. <prueba>";
        var xml = UnComprobante.Factura().ParaElCliente(razonSocial).ConLinea("Arroz", 1m, 18.00m).Xml();

        Assert.Contains("&amp; HIJOS", xml.Texto);
        Assert.Contains("&lt;prueba&gt;", xml.Texto);
        Assert.Equal(razonSocial, xml.Valor("cac:AccountingCustomerParty/cac:Party/cac:PartyTaxScheme/cbc:RegistrationName"));
    }

    [Fact]
    public void Los_importes_usan_punto_decimal_aunque_la_cultura_del_servidor_use_coma()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES"); // 1.234,56
            var xml = UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 1000m, 18.50m).Xml();

            Assert.Equal("18500.00", xml.Valor("cac:LegalMonetaryTotal/cbc:LineExtensionAmount"));
            Assert.Equal("1000.00", xml.Valor("cbc:InvoicedQuantity", xml.Lineas[0]));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void El_mismo_comprobante_genera_siempre_los_mismos_bytes()
    {
        var comprobante = UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 10m, 18.00m).Calculado();
        var generador = new UblInvoice21XmlSerializer();

        Assert.Equal(generador.GenerarXmlUbl21(comprobante), generador.GenerarXmlUbl21(comprobante));
    }

    [Fact]
    public void El_xml_generado_se_lee_de_vuelta_con_las_mismas_clases_generadas()
    {
        var bytes = UnComprobante.Factura().ConLinea("Arroz extra 5 kg", 10m, 18.00m).XmlGenerado();

        using var ms = new MemoryStream(bytes);
        var leido = Assert.IsType<InvoiceType>(new XmlSerializer(typeof(InvoiceType)).Deserialize(ms));

        Assert.Equal("F001-1", leido.ID!.Value);
        Assert.Equal("PEN", leido.LegalMonetaryTotal!.PayableAmount!.CurrencyID);
        Assert.Equal("212.40", leido.LegalMonetaryTotal.PayableAmount.Value);
        Assert.Equal("Arroz extra 5 kg", leido.InvoiceLine.Single().Item!.Description.Single().Value);
    }

    [Fact]
    public void Un_comprobante_sin_lineas_no_llega_a_generarse()
    {
        Assert.Throws<DominioValidationException>(() => UnComprobante.Factura().XmlGenerado());
    }
}
