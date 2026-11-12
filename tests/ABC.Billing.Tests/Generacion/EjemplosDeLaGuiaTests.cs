using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Generacion;

/// <summary>
/// Reproduce con nuestro generador los ejemplos de la "Guía de elaboración de documentos electrónicos XML – Factura
/// Electrónica UBL 2.1" (SUNAT, mayo 2017) que el sistema soporta por completo.
/// Ejemplo C de la guía (pág. 97): factura de transferencia gratuita FS21-4370 (un televisor entregado como premio).
/// Se usan RUC ficticios con Módulo 11 válido: los RUC impresos en la guía no pasan esa validación.
/// </summary>
public class EjemplosDeLaGuiaTests
{
    private static Comprobante EjemploC()
    {
        var emisor = new Emisor
        {
            Ruc = new Ruc(Fabrica.RucEmisor), RazonSocial = "Soporte Tecnológicos EIRL", NombreComercial = "Tu Soporte", CodigoEstablecimiento = "0001"
        };
        var cliente = new Cliente { TipoDocumento = TipoDocumentoIdentidad.RUC, NumeroDocumento = Fabrica.RucCliente, RazonSocial = "Boticas y Bazares S. A." };
        var c = new Comprobante
        {
            Emisor = emisor, Cliente = cliente, TipoComprobante = TipoComprobante.Factura, TipoOperacion = TipoOperacion.VentaInterna,
            Numeracion = new SerieComprobante("FS21", 4370), FechaEmision = new DateTime(2017, 6, 20, 9, 12, 31),
            Lineas = [Fabrica.Linea(1, "Televisor plasma de 42\", marca \"RCA\"", 1m, 1250.00m, AfectacionIgv.InafectoRetiroPorPremio)]
        };
        Fabrica.Calculadora.Calcular(c);
        return c;
    }

    private static readonly Comprobante Ejemplo = EjemploC();
    private static readonly byte[] Xml = Fabrica.XmlSinFirma(Ejemplo);
    private static System.Xml.Linq.XElement Raiz => Fabrica.Doc(Xml).Root!;
    private static System.Xml.Linq.XNamespace Cbc => Fabrica.Cbc;
    private static System.Xml.Linq.XNamespace Cac => Fabrica.Cac;

    [Fact]
    public void Ejemplo_C_cabecera_del_documento()
    {
        Assert.Equal("FS21-4370", Raiz.Element(Cbc + "ID")!.Value);
        Assert.Equal("2017-06-20", Raiz.Element(Cbc + "IssueDate")!.Value);
        Assert.Equal("09:12:31", Raiz.Element(Cbc + "IssueTime")!.Value);
        Assert.Equal("01", Raiz.Element(Cbc + "InvoiceTypeCode")!.Value);
        Assert.Equal("2.1", Raiz.Element(Cbc + "UBLVersionID")!.Value);
        Assert.Equal("2.0", Raiz.Element(Cbc + "CustomizationID")!.Value);
        Assert.Equal("PEN", Raiz.Element(Cbc + "DocumentCurrencyCode")!.Value);
        Assert.Equal("1", Raiz.Element(Cbc + "LineCountNumeric")!.Value);
    }

    [Fact]
    public void Ejemplo_C_lleva_la_leyenda_1002_de_transferencia_gratuita()
    {
        var nota = Raiz.Elements(Cbc + "Note").Single(n => n.Attribute("languageLocaleID")!.Value == "1002");
        Assert.Equal("TRANSFERENCIA GRATUITA DE UN BIEN Y/O SERVICIO PRESTADO GRATUITAMENTE", nota.Value);
    }

    [Fact]
    public void Ejemplo_C_emisor_con_nombre_comercial_razon_social_y_local_anexo()
    {
        var emisor = Raiz.Element(Cac + "AccountingSupplierParty")!;
        Assert.Equal("Tu Soporte", emisor.Descendants(Cac + "PartyName").Single().Element(Cbc + "Name")!.Value);
        Assert.Equal("Soporte Tecnológicos EIRL", emisor.Descendants(Cbc + "RegistrationName").Single().Value);
        Assert.Equal("0001", emisor.Descendants(Cbc + "AddressTypeCode").Single().Value);
        Assert.Equal("-", emisor.Descendants(Cac + "TaxScheme").Single().Element(Cbc + "ID")!.Value);
    }

    [Fact]
    public void Ejemplo_C_cliente()
    {
        var cliente = Raiz.Element(Cac + "AccountingCustomerParty")!;
        Assert.Equal("Boticas y Bazares S. A.", cliente.Descendants(Cbc + "RegistrationName").Single().Value);
        Assert.Equal("6", cliente.Descendants(Cbc + "CompanyID").Single().Attribute("schemeID")!.Value);
    }

    [Fact]
    public void Ejemplo_C_todos_los_totales_de_la_venta_son_cero_porque_es_gratuita()
    {
        var total = Raiz.Element(Cac + "LegalMonetaryTotal")!;
        Assert.Equal("0.00", total.Element(Cbc + "LineExtensionAmount")!.Value);
        Assert.Equal("0.00", total.Element(Cbc + "TaxInclusiveAmount")!.Value);
        Assert.Equal("0.00", total.Element(Cbc + "PayableAmount")!.Value);
        Assert.Equal("0.00", Raiz.Element(Cac + "TaxTotal")!.Element(Cbc + "TaxAmount")!.Value);
    }

    [Fact]
    public void Ejemplo_C_la_linea_informa_valor_referencial_1250_precio_cero_y_afectacion_35()
    {
        var linea = Raiz.Element(Cac + "InvoiceLine")!;
        Assert.Equal("1", linea.Element(Cbc + "InvoicedQuantity")!.Value);
        Assert.Equal("NIU", linea.Element(Cbc + "InvoicedQuantity")!.Attribute("unitCode")!.Value);
        Assert.Equal("0.00", linea.Element(Cbc + "LineExtensionAmount")!.Value);                        // valor de venta por ítem
        var referencia = linea.Element(Cac + "PricingReference")!.Element(Cac + "AlternativeConditionPrice")!;
        Assert.Equal("1250.00", referencia.Element(Cbc + "PriceAmount")!.Value);                         // valor referencial unitario
        Assert.Equal("02", referencia.Element(Cbc + "PriceTypeCode")!.Value);                            // Catálogo 16
        Assert.Equal("0.00", linea.Element(Cac + "Price")!.Element(Cbc + "PriceAmount")!.Value);         // valor unitario por ítem
        Assert.Equal("35", linea.Descendants(Cbc + "TaxExemptionReasonCode").Single().Value);            // Catálogo 07
        Assert.Equal("Televisor plasma de 42\", marca \"RCA\"", linea.Element(Cac + "Item")!.Element(Cbc + "Description")!.Value);
    }

    [Fact]
    public void Ejemplo_C_el_valor_de_venta_de_las_operaciones_gratuitas_es_1250()
    {
        Assert.Equal(1250.00m, Ejemplo.TotalOperacionesGratuitas);
        var subtotal = Raiz.Element(Cac + "TaxTotal")!.Element(Cac + "TaxSubtotal")!;
        Assert.Equal("1250.00", subtotal.Element(Cbc + "TaxableAmount")!.Value);
        Assert.Equal("0.00", subtotal.Element(Cbc + "TaxAmount")!.Value);
    }

    /// <summary>
    /// DIFERENCIA DOCUMENTADA: en el XML de su ejemplo C la guía agrupa esta operación gratuita bajo 9998 INAFECTO, pero
    /// los numerales 24, 25 y 26 de su texto dicen que las gratuitas NO se suman a exoneradas ni a inafectas y se informan
    /// con el tributo 9996 GRATUITO (categoría Z, tipo FRE). El generador sigue el texto de los numerales.
    /// </summary>
    [Fact]
    public void Ejemplo_C_las_gratuitas_se_informan_como_9996_GRATUITO_segun_el_numeral_26_de_la_guia()
    {
        var esquema = Raiz.Element(Cac + "TaxTotal")!.Descendants(Cac + "TaxScheme").Single();
        Assert.Equal("9996", esquema.Element(Cbc + "ID")!.Value);
        Assert.Equal("GRATUITO", esquema.Element(Cbc + "Name")!.Value);
        Assert.Equal("FRE", esquema.Element(Cbc + "TaxTypeCode")!.Value);
        Assert.Equal("Z", Raiz.Element(Cac + "TaxTotal")!.Descendants(Cac + "TaxCategory").Single().Element(Cbc + "ID")!.Value);
        Assert.Equal(0m, Ejemplo.TotalOperacionesInafectas);   // no se suma a inafectas (numeral 25)
    }

    [Fact]
    public void Ejemplo_C_firmado_cumple_el_XSD_oficial()
        => Fabrica.Validador.Validar(Fabrica.Firmador.Firmar(Xml).Xml);
}
