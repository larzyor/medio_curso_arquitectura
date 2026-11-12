using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.UblEngine.Generado;

namespace ABC.Billing.Infrastructure.UblEngine.Generacion;

/// <summary>
/// Adaptador de salida: traduce el Comprobante del dominio (comprobante, líneas, tributos, cliente) a las clases
/// GENERADAS desde UBL-Invoice-2.1.xsd (carpeta Generado) y las serializa con XmlSerializer.
/// Es el ÚNICO lugar que conoce las etiquetas UBL; el dominio no sabe nada de XML. El escape de &amp;, &lt; y comillas
/// lo hace el serializador (no se concatenan cadenas). Salida en ISO-8859-1, como exige el Manual del Programador.
/// </summary>
public sealed class GeneradorXmlUblFactura21 : IGeneradorXmlUbl
{
    public const string DeclaracionXml = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>\n";

    private static readonly XmlSerializer Serializador = new(typeof(InvoiceType));
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Unece = "United Nations Economic Commission for Europe";

    public byte[] GenerarXmlUbl21(Comprobante comprobante)
    {
        var documento = Mapear(comprobante);

        var espacios = new XmlSerializerNamespaces();
        espacios.Add("", UblNs.Invoice);
        espacios.Add("cac", UblNs.Cac);
        espacios.Add("cbc", UblNs.Cbc);
        espacios.Add("ext", UblNs.Ext);
        espacios.Add("ds", UblNs.Ds);

        using var ms = new MemoryStream();
        ms.Write(Encoding.Latin1.GetBytes(DeclaracionXml)); // declaración tal como la muestra el Manual de SUNAT
        var opciones = new XmlWriterSettings { Encoding = Encoding.Latin1, OmitXmlDeclaration = true, Indent = true, IndentChars = "  " };
        using (var w = XmlWriter.Create(ms, opciones))
            Serializador.Serialize(w, documento, espacios);
        return ms.ToArray();
    }

    /// <summary>Dominio → clases generadas del XSD (visible para las pruebas unitarias de generación).</summary>
    public static InvoiceType Mapear(Comprobante c)
    {
        string mon = c.Moneda.ToString();

        var f = new InvoiceType
        {
            UBLExtensions = new UBLExtensionsType { UBLExtension = { new UBLExtensionType { ExtensionContent = new ExtensionContentType() } } }, // aquí irá la firma
            UBLVersionID = new UBLVersionIDType { Value = "2.1" },
            CustomizationID = new CustomizationIDType { Value = "2.0" },
            ProfileID = new ProfileIDType
            { Value = c.TipoOperacion.Codigo(), SchemeName = "Tipo de Operacion", SchemeAgencyName = UblNs.Agency, SchemeURI = UblNs.Catalogo("51") },
            ID = new IDType { Value = c.SerieNumero },
            IssueDate = new IssueDateType { Value = c.FechaEmision.ToString("yyyy-MM-dd", Inv) },
            IssueTime = new IssueTimeType { Value = c.FechaEmision.ToString("HH:mm:ss", Inv) },
            InvoiceTypeCode = new InvoiceTypeCodeType
            { Value = c.TipoComprobante.Codigo(), ListAgencyName = UblNs.Agency, ListName = "Tipo de Documento", ListURI = UblNs.Catalogo("01") },
            DocumentCurrencyCode = new DocumentCurrencyCodeType { Value = mon, ListID = "ISO 4217 Alpha", ListName = "Currency", ListAgencyName = Unece },
            AccountingSupplierParty = new SupplierPartyType { Party = ParteEmisor(c.Emisor) },
            AccountingCustomerParty = new CustomerPartyType { Party = ParteCliente(c.Cliente) },
            LegalMonetaryTotal = new MonetaryTotalType
            {
                LineExtensionAmount = new LineExtensionAmountType { Value = Dos(c.TotalValorVenta), CurrencyID = mon },
                TaxInclusiveAmount = new TaxInclusiveAmountType { Value = Dos(c.TotalPrecioVenta), CurrencyID = mon },
                PayableAmount = new PayableAmountType { Value = Dos(c.ImporteTotalPagar), CurrencyID = mon }
            }
        };

        f.Note.Add(new NoteType { Value = MontoEnLetras.Convertir(c.ImporteTotalPagar, c.Moneda), LanguageLocaleID = Leyenda.MontoEnLetras });
        if (c.Lineas.All(l => l.AfectacionIgv.EsGratuita()))
            f.Note.Add(new NoteType { Value = "TRANSFERENCIA GRATUITA DE UN BIEN Y/O SERVICIO PRESTADO GRATUITAMENTE", LanguageLocaleID = Leyenda.TransferenciaGratuita });

        // cac:Signature identifica al firmante y apunta a la firma que se coloca en ext:UBLExtensions
        f.Signature.Add(new SignatureType
        {
            ID = new IDType { Value = UblNs.SignatureId },
            SignatoryParty = new PartyType
            {
                PartyIdentification = { new PartyIdentificationType { ID = new IDType { Value = c.Emisor.Ruc.Value } } },
                PartyName = { new PartyNameType { Name = new NameType { Value = c.Emisor.RazonSocial } } }
            },
            DigitalSignatureAttachment = new AttachmentType
            { ExternalReference = new ExternalReferenceType { URI = new URIType { Value = "#" + UblNs.SignatureId } } }
        });

        // Un solo TaxTotal de cabecera con un subtotal por tributo (Catálogo 05)
        var total = new TaxTotalType { TaxAmount = new TaxAmountType { Value = Dos(c.TotalTributos), CurrencyID = mon } };
        foreach (var t in c.Tributos)
            total.TaxSubtotal.Add(new TaxSubtotalType
            {
                TaxableAmount = t.BaseImponible is null ? null : new TaxableAmountType { Value = Dos(t.BaseImponible.Value), CurrencyID = mon },
                TaxAmount = new TaxAmountType { Value = Dos(t.Monto), CurrencyID = mon },
                TaxCategory = Categoria(t.Tributo)
            });
        f.TaxTotal.Add(total);

        foreach (var l in c.Lineas) f.InvoiceLine.Add(MapearLinea(l, mon));
        return f;
    }

    /// <summary>Los importes se escriben siempre con al menos 2 decimales (180.00, no 180).</summary>
    private static decimal Dos(decimal v) => v + 0.00m;

    private static PartyType ParteEmisor(Emisor e) => new()
    {
        PartyName = { new PartyNameType { Name = new NameType { Value = e.NombreComercial ?? e.RazonSocial } } },
        PartyTaxScheme =
        {
            new PartyTaxSchemeType
            {
                RegistrationName = new RegistrationNameType { Value = e.RazonSocial },
                CompanyID = Documento(TipoDocumentoIdentidad.RUC, e.Ruc.Value),
                RegistrationAddress = new AddressType
                { AddressTypeCode = new AddressTypeCodeType { Value = e.CodigoEstablecimiento, ListAgencyName = UblNs.Agency, ListName = "Establecimientos anexos" } },
                TaxScheme = new TaxSchemeType { ID = new IDType { Value = "-" } }
            }
        }
    };

    private static PartyType ParteCliente(Cliente c) => new()
    {
        PartyTaxScheme =
        {
            new PartyTaxSchemeType
            {
                RegistrationName = new RegistrationNameType { Value = c.RazonSocial },
                CompanyID = Documento(c.TipoDocumento, c.NumeroDocumento),
                TaxScheme = new TaxSchemeType { ID = new IDType { Value = "-" } }
            }
        }
    };

    private static CompanyIDType Documento(TipoDocumentoIdentidad tipo, string numero) => new()
    { Value = numero, SchemeID = tipo.Codigo(), SchemeName = "Documento de Identidad", SchemeAgencyName = UblNs.Agency, SchemeURI = UblNs.Catalogo("06") };

    private static InvoiceLineType MapearLinea(LineaComprobante l, string mon)
    {
        bool gratuita = l.AfectacionIgv.EsGratuita();
        var linea = new InvoiceLineType
        {
            ID = new IDType { Value = l.NumeroLinea.ToString(Inv) },
            InvoicedQuantity = new InvoicedQuantityType
            { Value = l.Cantidad, UnitCode = l.UnidadMedida.ToString(), UnitCodeListID = "UN/ECE rec 20", UnitCodeListAgencyName = Unece },
            LineExtensionAmount = new LineExtensionAmountType { Value = Dos(l.ValorVenta), CurrencyID = mon },
            Item = new ItemType
            {
                Description = { new DescriptionType { Value = l.Descripcion } },
                SellersItemIdentification = string.IsNullOrWhiteSpace(l.CodigoProducto) ? null : new ItemIdentificationType { ID = new IDType { Value = l.CodigoProducto } }
            },
            Price = new PriceType { PriceAmount = new PriceAmountType { Value = Dos(gratuita ? 0m : l.ValorUnitario), CurrencyID = mon } }
        };

        // Precio de venta unitario (Catálogo 16): 01 incluye impuestos; 02 = valor referencial (gratuitas)
        linea.PricingReference = new PricingReferenceType
        {
            AlternativeConditionPrice =
            {
                new PriceType
                {
                    PriceAmount = new PriceAmountType { Value = Dos(gratuita ? l.ValorUnitario : l.PrecioUnitarioConImpuestos), CurrencyID = mon },
                    PriceTypeCode = new PriceTypeCodeType
                    {
                        Value = (gratuita ? TipoPrecio.ValorReferencialOperacionNoOnerosa : TipoPrecio.PrecioUnitarioIncluyeIgv).Codigo(),
                        ListName = "Tipo de Precio", ListAgencyName = UblNs.Agency, ListURI = UblNs.Catalogo("16")
                    }
                }
            }
        };

        // Orden de cálculo: ISC primero, luego IGV; el ICBPER al final
        var impuestos = new TaxTotalType();
        if (l.MontoIsc > 0 && l.SistemaIsc is not null)
        {
            decimal? porcentaje = l.SistemaIsc == SistemaIsc.AlValor ? l.TasaIsc : null;
            impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Isc, l.ValorVenta, l.MontoIsc, mon, porcentaje, null, tier: l.SistemaIsc.Value.Codigo()));
        }

        var afectacion = l.AfectacionIgv;
        switch (afectacion.Categoria())
        {
            case CategoriaAfectacion.GravadoOneroso:
                impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Igv, l.BaseIgv, l.MontoIgv, mon, 18m, afectacion)); break;
            case CategoriaAfectacion.GravadoGratuito:
                impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Gratuito, l.ValorReferencialTotal, l.MontoIgv, mon, 18m, afectacion)); break;
            case CategoriaAfectacion.ExoneradoOneroso:
                impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Exonerado, l.ValorVenta, 0m, mon, null, afectacion)); break;
            case CategoriaAfectacion.InafectoOneroso:
                impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Inafecto, l.ValorVenta, 0m, mon, null, afectacion)); break;
            case CategoriaAfectacion.Exportacion:
                impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Exportacion, l.ValorVenta, 0m, mon, null, afectacion)); break;
            default: // exonerado / inafecto gratuitos
                impuestos.TaxSubtotal.Add(Subtotal(TributoSunat.Gratuito, l.ValorReferencialTotal, 0m, mon, null, afectacion)); break;
        }

        if (l.AplicaIcbper)
        {
            impuestos.TaxSubtotal.Add(new TaxSubtotalType
            {
                TaxAmount = new TaxAmountType { Value = Dos(l.MontoIcbper), CurrencyID = mon },
                BaseUnitMeasure = new BaseUnitMeasureType { Value = l.Cantidad, UnitCode = "NIU" },
                TaxCategory = Categoria(TributoSunat.Icbper, perUnit: new PerUnitAmountType { Value = Dos(l.TarifaIcbper), CurrencyID = mon })
            });
        }

        impuestos.TaxAmount = new TaxAmountType { Value = Dos(impuestos.TaxSubtotal.Sum(s => s.TaxAmount.Value)), CurrencyID = mon };
        linea.TaxTotal.Add(impuestos);
        return linea;
    }

    private static TaxSubtotalType Subtotal(TributoSunat t, decimal @base, decimal monto, string mon, decimal? porcentaje, AfectacionIgv? afectacion, string? tier = null) => new()
    {
        TaxableAmount = new TaxableAmountType { Value = Dos(@base), CurrencyID = mon },
        TaxAmount = new TaxAmountType { Value = Dos(monto), CurrencyID = mon },
        TaxCategory = Categoria(t, porcentaje, afectacion, tier)
    };

    private static TaxCategoryType Categoria(TributoSunat t, decimal? porcentaje = null, AfectacionIgv? afectacion = null, string? tier = null, PerUnitAmountType? perUnit = null) => new()
    {
        ID = new IDType { Value = t.CategoriaUbl, SchemeID = "UN/ECE 5305", SchemeName = "Tax Category Identifier", SchemeAgencyName = Unece },
        Percent = porcentaje is null ? null : new PercentType { Value = Dos(porcentaje.Value) },
        PerUnitAmount = perUnit,
        TaxExemptionReasonCode = afectacion is null ? null : new TaxExemptionReasonCodeType
        { Value = afectacion.Value.Codigo(), ListAgencyName = UblNs.Agency, ListName = "Afectacion del IGV", ListURI = UblNs.Catalogo("07") },
        // cbc:TierRange es de tipo texto en UBL 2.1: no admite atributos de catálogo (el XSD los rechaza)
        TierRange = tier is null ? null : new TierRangeType { Value = tier },
        TaxScheme = new TaxSchemeType
        {
            ID = new IDType { Value = t.Codigo, SchemeName = "Codigo de tributos", SchemeAgencyName = UblNs.Agency, SchemeURI = UblNs.Catalogo("05") },
            Name = new NameType { Value = t.Nombre },
            TaxTypeCode = new TaxTypeCodeType { Value = t.CodigoInternacional }
        }
    };
}
