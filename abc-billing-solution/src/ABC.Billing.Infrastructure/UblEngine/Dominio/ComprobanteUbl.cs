using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Infrastructure.UblEngine.Generated;
using ABC.Billing.Infrastructure.UblEngine.Model;
using static ABC.Billing.Infrastructure.UblEngine.Dominio.VocabularioUbl;

namespace ABC.Billing.Infrastructure.UblEngine.Dominio;

/// <summary>
/// El comprobante electrónico contado en el lenguaje del dominio: quién lo emite, a qué cliente, qué leyendas,
/// qué tributos, qué totales y qué líneas lleva. Por dentro llena las clases que se GENERARON desde
/// UBL-Invoice-2.1.xsd (carpeta Generated), así que el orden de los elementos y sus namespaces los decide el XSD
/// oficial, no este código.
/// <code>
/// var xml = ComprobanteUbl.Nuevo(TipoComprobante.Factura, "F001-1", fecha, TipoMoneda.PEN)
///     .DeTipoDeOperacion(TipoOperacion.VentaInterna)
///     .EmitidoPor(emisor)
///     .ParaElCliente(cliente)
///     .ConLeyenda(Leyenda.MontoEnLetras, "SON: CIEN Y 00/100 SOLES")
///     .ConTributos(totalTributos, subtotales)
///     .ConTotales(valorVenta, precioVenta, importeAPagar)
///     .ConLinea(linea)
///     .ComoXml();
/// </code>
/// </summary>
public sealed class ComprobanteUbl
{
    /// <summary>La declaración se escribe tal como la muestra el Manual de SUNAT (ISO-8859-1 en mayúsculas).</summary>
    public const string DeclaracionXml = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>\n";

    private static readonly XmlSerializer Serializador = new(typeof(InvoiceType));

    private readonly InvoiceType _documento;
    private readonly TipoMoneda _moneda;

    private ComprobanteUbl(InvoiceType documento, TipoMoneda moneda)
    {
        _documento = documento;
        _moneda = moneda;
    }

    /// <summary>Modelo UBL (clases generadas desde el XSD). Útil para inspeccionarlo en pruebas.</summary>
    public InvoiceType Documento => _documento;

    // ------------------------------------------------------------------------------------------- de punta a punta

    /// <summary>Traduce un comprobante ya calculado (<c>TaxCalculatorService</c>) con todo su contenido.</summary>
    public static ComprobanteUbl Desde(Invoice comprobante)
    {
        var ubl = Nuevo(comprobante.TipoComprobante, comprobante.SerieNumero, comprobante.FechaEmision, comprobante.Moneda)
            .DeTipoDeOperacion(comprobante.TipoOperacion)
            .ConLeyenda(Leyenda.MontoEnLetras, MontoEnLetras.Convertir(comprobante.ImporteTotalPagar, comprobante.Moneda));

        if (comprobante.Items.Count > 0 && comprobante.Items.All(i => i.AfectacionIgv.EsGratuita()))
            ubl.ConLeyenda(Leyenda.TransferenciaGratuita, "TRANSFERENCIA GRATUITA DE UN BIEN Y/O SERVICIO PRESTADO GRATUITAMENTE");

        ubl.EmitidoPor(comprobante.Emisor)
           .ParaElCliente(comprobante.Cliente)
           .ConTributos(comprobante.TotalTributos, comprobante.Tributos)
           .ConTotales(comprobante.TotalValorVenta, comprobante.TotalPrecioVenta, comprobante.ImporteTotalPagar);

        foreach (var linea in comprobante.Items) ubl.ConLinea(linea);
        return ubl;
    }

    // ------------------------------------------------------------------------------------------- vocabulario

    /// <summary>Encabezado: tipo de comprobante (Catálogo 01), serie-número, fecha y hora de emisión y moneda.</summary>
    public static ComprobanteUbl Nuevo(TipoComprobante tipo, string serieNumero, DateTime fechaEmision, TipoMoneda moneda)
    {
        // Espacio para la firma digital: el firmador inserta ds:Signature dentro de ExtensionContent.
        var extensiones = new UBLExtensionsType();
        extensiones.UBLExtension.Add(new UBLExtensionType { ExtensionContent = new ExtensionContentType() });

        var documento = new InvoiceType
        {
            UBLExtensions = extensiones,
            UBLVersionID = Identificador("2.1"),
            CustomizationID = Identificador("2.0"),
            ID = Identificador(serieNumero),
            IssueDate = Fecha(fechaEmision),
            IssueTime = Hora(fechaEmision),
            InvoiceTypeCode = CodigoDeCatalogo(tipo.Codigo(), "Tipo de Documento", "01"),
            DocumentCurrencyCode = Moneda(moneda)
        };
        return new ComprobanteUbl(documento, moneda);
    }

    /// <summary>Tipo de operación (Catálogo 51), p. ej. 0101 venta interna.</summary>
    public ComprobanteUbl DeTipoDeOperacion(TipoOperacion operacion)
    {
        _documento.ProfileID = IdentificadorDeCatalogo(operacion.Codigo(), "Tipo de Operacion", "51");
        return this;
    }

    /// <summary>Leyenda del Catálogo 52 (1000 = monto en letras, 1002 = transferencia gratuita...).</summary>
    public ComprobanteUbl ConLeyenda(string codigoLeyenda, string texto)
    {
        _documento.Note.Add(new TextType { Value = texto, LanguageLocaleID = codigoLeyenda });
        return this;
    }

    /// <summary>
    /// Emisor: razón social, nombre comercial, RUC y establecimiento anexo. También queda como firmante del
    /// comprobante (cac:Signature apunta a la firma que se guarda en las extensiones).
    /// </summary>
    public ComprobanteUbl EmitidoPor(Emisor emisor)
    {
        var firmante = new PartyType();
        firmante.PartyIdentification.Add(new PartyIdentificationType { ID = Identificador(emisor.Ruc.Value) });
        firmante.PartyName.Add(new PartyNameType { Name = Nombre(emisor.RazonSocial) });
        var firma = new SignatureType
        {
            ID = Identificador(UblNs.SignatureId),
            SignatoryParty = firmante,
            DigitalSignatureAttachment = new AttachmentType
            {
                ExternalReference = new ExternalReferenceType { URI = Identificador("#" + UblNs.SignatureId) }
            }
        };
        _documento.Signature.Clear();
        _documento.Signature.Add(firma);

        var empresa = new PartyType();
        empresa.PartyName.Add(new PartyNameType { Name = Nombre(emisor.NombreComercial ?? emisor.RazonSocial) });
        empresa.PartyTaxScheme.Add(new PartyTaxSchemeType
        {
            RegistrationName = Nombre(emisor.RazonSocial),
            CompanyID = DocumentoDeIdentidad(TipoDocumentoIdentidad.RUC, emisor.Ruc.Value),
            RegistrationAddress = new AddressType
            {
                AddressTypeCode = new CodeType
                {
                    Value = emisor.CodigoEstablecimiento,
                    ListAgencyName = UblNs.Agency,
                    ListName = "Establecimientos anexos"
                }
            },
            TaxScheme = SinEsquemaTributario()
        });
        _documento.AccountingSupplierParty = new SupplierPartyType { Party = empresa };
        return this;
    }

    /// <summary>Cliente (adquirente): nombre o razón social y su documento de identidad (Catálogo 06).</summary>
    public ComprobanteUbl ParaElCliente(Customer cliente)
    {
        var adquirente = new PartyType();
        adquirente.PartyTaxScheme.Add(new PartyTaxSchemeType
        {
            RegistrationName = Nombre(cliente.RazonSocial),
            CompanyID = DocumentoDeIdentidad(cliente.TipoDocumento, cliente.NumeroDocumento),
            TaxScheme = SinEsquemaTributario()
        });
        _documento.AccountingCustomerParty = new CustomerPartyType { Party = adquirente };
        return this;
    }

    /// <summary>
    /// Resumen de tributos del comprobante: UN cac:TaxTotal con el total y un subtotal por tributo del Catálogo 05
    /// (IGV, ISC, exonerado, inafecto, gratuito, ICBPER...), en el orden que entrega el cálculo.
    /// </summary>
    public ComprobanteUbl ConTributos(decimal totalTributos, IEnumerable<TaxSubtotal> subtotales)
    {
        var resumen = new TaxTotalType { TaxAmount = Importe(totalTributos, _moneda) };
        foreach (var t in subtotales)
        {
            resumen.TaxSubtotal.Add(new TaxSubtotalType
            {
                TaxableAmount = t.BaseImponible is null ? null : Importe(t.BaseImponible.Value, _moneda),
                TaxAmount = Importe(t.Monto, _moneda),
                TaxCategory = CategoriaTributaria(t.Tributo)
            });
        }
        _documento.TaxTotal.Clear();
        _documento.TaxTotal.Add(resumen);
        return this;
    }

    /// <summary>Totales: valor de venta (sin impuestos), precio de venta (con impuestos) e importe total a pagar.</summary>
    public ComprobanteUbl ConTotales(decimal valorVenta, decimal precioVenta, decimal importeAPagar)
    {
        _documento.LegalMonetaryTotal = new MonetaryTotalType
        {
            LineExtensionAmount = Importe(valorVenta, _moneda),
            TaxInclusiveAmount = Importe(precioVenta, _moneda),
            PayableAmount = Importe(importeAPagar, _moneda)
        };
        return this;
    }

    /// <summary>Agrega un ítem (ya calculado) con sus tributos de línea.</summary>
    public ComprobanteUbl ConLinea(InvoiceLine linea)
    {
        _documento.InvoiceLine.Add(LineaUbl.Desde(linea, _moneda));
        return this;
    }

    // ------------------------------------------------------------------------------------------- salida

    /// <summary>
    /// XML UBL 2.1 en ISO-8859-1. Lo escribe XmlSerializer a partir de las clases generadas: el escape de &amp;, &lt;
    /// y comillas lo hace el serializador; no se concatenan cadenas.
    /// </summary>
    public byte[] ComoXml()
    {
        var prefijos = new XmlSerializerNamespaces();
        prefijos.Add("", UblXmlNs.Invoice);
        prefijos.Add("cac", UblXmlNs.Cac);
        prefijos.Add("cbc", UblXmlNs.Cbc);
        prefijos.Add("ext", UblXmlNs.Ext);
        prefijos.Add("ds", UblNs.Ds);

        using var ms = new MemoryStream();
        ms.Write(Encoding.Latin1.GetBytes(DeclaracionXml));
        var opciones = new XmlWriterSettings { Encoding = Encoding.Latin1, OmitXmlDeclaration = true, Indent = true, IndentChars = "  " };
        using (var w = XmlWriter.Create(ms, opciones))
            Serializador.Serialize(w, _documento, prefijos);
        return ms.ToArray();
    }

    /// <summary>cac:TaxScheme dentro de PartyTaxScheme: solo cbc:ID = "-" (la guía SUNAT no usa un tributo aquí).</summary>
    private static TaxSchemeType SinEsquemaTributario() => new() { ID = Identificador("-") };
}
