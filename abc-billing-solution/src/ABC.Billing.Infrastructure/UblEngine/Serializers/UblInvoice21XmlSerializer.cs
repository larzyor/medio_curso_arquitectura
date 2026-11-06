using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.UblEngine.Model;

namespace ABC.Billing.Infrastructure.UblEngine.Serializers;

/// <summary>
/// Adaptador de salida: convierte la entidad Invoice del dominio en el XML UBL 2.1 de SUNAT.
/// Construye CLASES (UblInvoice) y las serializa con XmlSerializer: el escape de &amp;, &lt; y comillas lo hace
/// el serializador; no se concatenan cadenas. Salida en ISO-8859-1, como exige el Manual del Programador (1.4.c).
/// </summary>
public sealed class UblInvoice21XmlSerializer : IUblXmlGenerator
{
    private static readonly XmlSerializer Serializer = new(typeof(UblInvoice));
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string UneceAgency = "United Nations Economic Commission for Europe";

    public byte[] GenerarXmlUbl21(Invoice inv)
    {
        var modelo = Mapear(inv);

        var ns = new XmlSerializerNamespaces();
        ns.Add("", UblNs.Invoice);
        ns.Add("cac", UblNs.Cac);
        ns.Add("cbc", UblNs.Cbc);
        ns.Add("ext", UblNs.Ext);
        ns.Add("ds", UblNs.Ds);

        using var ms = new MemoryStream();
        // La declaración se escribe tal como la muestra el Manual de SUNAT (ISO-8859-1 en mayúsculas).
        ms.Write(Encoding.Latin1.GetBytes(DeclaracionXml));
        var settings = new XmlWriterSettings { Encoding = Encoding.Latin1, OmitXmlDeclaration = true, Indent = true, IndentChars = "  " };
        using (var w = XmlWriter.Create(ms, settings))
            Serializer.Serialize(w, modelo, ns);
        return ms.ToArray();
    }

    public const string DeclaracionXml = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>\n";

    internal static UblInvoice Mapear(Invoice inv)
    {
        string cur = inv.Moneda.ToString();
        var m = new UblInvoice();

        m.Extensions.Items.Add(new UblExtension()); // aquí irá la firma
        m.ProfileId = new Cbc(inv.TipoOperacion.Codigo())
        { SchemeName = "Tipo de Operacion", SchemeAgencyName = UblNs.Agency, SchemeUri = UblNs.Catalogo("51") };
        m.Id = new Cbc(inv.SerieNumero);
        m.IssueDate = new Cbc(inv.FechaEmision.ToString("yyyy-MM-dd", Inv));
        m.IssueTime = new Cbc(inv.FechaEmision.ToString("HH:mm:ss", Inv));
        m.InvoiceTypeCode = new Cbc(inv.TipoComprobante.Codigo())
        { ListAgencyName = UblNs.Agency, ListName = "Tipo de Documento", ListUri = UblNs.Catalogo("01") };

        m.Notes.Add(new Cbc(MontoEnLetras.Convertir(inv.ImporteTotalPagar, inv.Moneda)) { LanguageLocaleId = Leyenda.MontoEnLetras });
        if (inv.Items.All(i => i.AfectacionIgv.EsGratuita()))
            m.Notes.Add(new Cbc("TRANSFERENCIA GRATUITA DE UN BIEN Y/O SERVICIO PRESTADO GRATUITAMENTE") { LanguageLocaleId = Leyenda.TransferenciaGratuita });

        m.DocumentCurrencyCode = new Cbc(cur) { ListId = "ISO 4217 Alpha", ListName = "Currency", ListAgencyName = UneceAgency };

        // cac:Signature: identifica al firmante y apunta a la firma que se coloca en ext:UBLExtensions.
        m.Signature.Id = new Cbc(UblNs.SignatureId);
        m.Signature.SignatoryParty.PartyIdentification = new UblPartyIdentification { Id = new Cbc(inv.Emisor.Ruc.Value) };
        m.Signature.SignatoryParty.PartyName = new UblPartyName { Name = new Cbc(inv.Emisor.RazonSocial) };
        m.Signature.Attachment.ExternalReference.Uri = new Cbc("#" + UblNs.SignatureId);

        // Emisor
        m.Supplier.Party.PartyName = new UblPartyName { Name = new Cbc(inv.Emisor.NombreComercial ?? inv.Emisor.RazonSocial) };
        m.Supplier.Party.PartyTaxScheme = new UblPartyTaxScheme
        {
            RegistrationName = new Cbc(inv.Emisor.RazonSocial),
            CompanyId = DocumentoIdentidad(TipoDocumentoIdentidad.RUC, inv.Emisor.Ruc.Value),
            RegistrationAddress = new UblRegistrationAddress
            {
                AddressTypeCode = new Cbc(inv.Emisor.CodigoEstablecimiento)
                { ListAgencyName = UblNs.Agency, ListName = "Establecimientos anexos" }
            }
        };

        // Adquirente
        m.Customer.Party.PartyTaxScheme = new UblPartyTaxScheme
        {
            RegistrationName = new Cbc(inv.Cliente.RazonSocial),
            CompanyId = DocumentoIdentidad(inv.Cliente.TipoDocumento, inv.Cliente.NumeroDocumento)
        };

        // Totales de impuestos del encabezado (un solo TaxTotal con varios TaxSubtotal)
        m.TaxTotal.TaxAmount = Importe(inv.TotalTributos, cur);
        foreach (var t in inv.Tributos)
        {
            var sub = new UblTaxSubtotal
            {
                TaxableAmount = t.BaseImponible is null ? null : Importe(t.BaseImponible.Value, cur),
                TaxAmount = Importe(t.Monto, cur),
                TaxCategory = Categoria(t.Tributo, null, null)
            };
            m.TaxTotal.Subtotals.Add(sub);
        }

        m.LegalMonetaryTotal.LineExtensionAmount = Importe(inv.TotalValorVenta, cur);
        m.LegalMonetaryTotal.TaxInclusiveAmount = Importe(inv.TotalPrecioVenta, cur);
        m.LegalMonetaryTotal.PayableAmount = Importe(inv.ImporteTotalPagar, cur);

        foreach (var l in inv.Items) m.Lines.Add(MapearLinea(l, cur));
        return m;
    }

    private static UblInvoiceLine MapearLinea(InvoiceLine l, string cur)
    {
        bool gratuita = l.AfectacionIgv.EsGratuita();
        var linea = new UblInvoiceLine
        {
            Id = new Cbc(l.NumeroLinea.ToString(Inv)),
            InvoicedQuantity = Cantidad(l.Cantidad, l.UnidadMedida.ToString()),
            LineExtensionAmount = Importe(l.ValorVenta, cur)
        };

        linea.PricingReference.Price.PriceAmount = Importe(gratuita ? l.ValorUnitario : l.PrecioUnitarioConImpuestos, cur, hastaDiezDecimales: true);
        linea.PricingReference.Price.PriceTypeCode = new Cbc((gratuita ? TipoPrecio.ValorReferencialOperacionNoOnerosa : TipoPrecio.PrecioUnitarioIncluyeIgv).Codigo())
        { ListName = "Tipo de Precio", ListAgencyName = UblNs.Agency, ListUri = UblNs.Catalogo("16") };

        // Orden de cálculo: ISC primero, luego IGV; el ICBPER al final.
        var subs = linea.TaxTotal.Subtotals;
        if (l.MontoIsc > 0 && l.SistemaIsc is not null)
        {
            decimal? porcentaje = l.SistemaIsc == SistemaIsc.AlValor ? l.TasaIsc : null;
            subs.Add(new UblTaxSubtotal
            {
                TaxableAmount = Importe(l.ValorVenta, cur),
                TaxAmount = Importe(l.MontoIsc, cur),
                TaxCategory = Categoria(TributoSunat.Isc, porcentaje, null, tier: l.SistemaIsc.Value.Codigo())
            });
        }

        var afect = l.AfectacionIgv;
        switch (afect.Categoria())
        {
            case CategoriaAfectacion.GravadoOneroso:
                subs.Add(Sub(TributoSunat.Igv, l.BaseIgv, l.MontoIgv, cur, 18m, afect));
                break;
            case CategoriaAfectacion.GravadoGratuito:
                subs.Add(Sub(TributoSunat.Gratuito, l.ValorReferencialTotal, l.MontoIgv, cur, 18m, afect));
                break;
            case CategoriaAfectacion.ExoneradoOneroso:
                subs.Add(Sub(TributoSunat.Exonerado, l.ValorVenta, 0m, cur, null, afect));
                break;
            case CategoriaAfectacion.InafectoOneroso:
                subs.Add(Sub(TributoSunat.Inafecto, l.ValorVenta, 0m, cur, null, afect));
                break;
            case CategoriaAfectacion.Exportacion:
                subs.Add(Sub(TributoSunat.Exportacion, l.ValorVenta, 0m, cur, null, afect));
                break;
            default: // exonerado/inafecto gratuitos
                subs.Add(Sub(TributoSunat.Gratuito, l.ValorReferencialTotal, 0m, cur, null, afect));
                break;
        }

        if (l.AplicaIcbper)
        {
            subs.Add(new UblTaxSubtotal
            {
                TaxAmount = Importe(l.MontoIcbper, cur),
                BaseUnitMeasure = new Cbc(l.Cantidad.ToString("0", Inv)) { UnitCode = "NIU" },
                TaxCategory = Categoria(TributoSunat.Icbper, null, Importe(l.TarifaIcbper, cur))
            });
        }

        linea.TaxTotal.TaxAmount = Importe(subs.Sum(s => decimal.Parse(s.TaxAmount.Value, Inv)), cur);

        linea.Item.Description = new Cbc(l.Descripcion);
        if (!string.IsNullOrWhiteSpace(l.CodigoProducto))
            linea.Item.SellersItemIdentification = new UblSellersItemIdentification { Id = new Cbc(l.CodigoProducto) };

        linea.Price.PriceAmount = Importe(gratuita ? 0m : l.ValorUnitario, cur, hastaDiezDecimales: true);
        return linea;
    }

    private static UblTaxSubtotal Sub(TributoSunat t, decimal baseImp, decimal monto, string cur, decimal? porcentaje, AfectacionIgv afect)
        => new()
        {
            TaxableAmount = Importe(baseImp, cur),
            TaxAmount = Importe(monto, cur),
            TaxCategory = Categoria(t, porcentaje, null, afect: afect)
        };

    private static UblTaxCategory Categoria(TributoSunat t, decimal? porcentaje, Cbc? perUnit, AfectacionIgv? afect = null, string? tier = null)
        => new()
        {
            Id = new Cbc(t.CategoriaUbl) { SchemeId = "UN/ECE 5305", SchemeName = "Tax Category Identifier", SchemeAgencyName = UneceAgency },
            Percent = porcentaje is null ? null : new Cbc(porcentaje.Value.ToString("0.00", Inv)),
            PerUnitAmount = perUnit,
            TaxExemptionReasonCode = afect is null ? null : new Cbc(afect.Value.Codigo())
            { ListAgencyName = UblNs.Agency, ListName = "Afectacion del IGV", ListUri = UblNs.Catalogo("07") },
            // cbc:TierRange es de tipo texto en UBL 2.1: no admite atributos de catálogo (el XSD los rechaza).
            TierRange = tier is null ? null : new Cbc(tier),
            TaxScheme = new UblTaxScheme
            {
                Id = new Cbc(t.Codigo) { SchemeName = "Codigo de tributos", SchemeAgencyName = UblNs.Agency, SchemeUri = UblNs.Catalogo("05") },
                Name = new Cbc(t.Nombre),
                TaxTypeCode = new Cbc(t.CodigoInternacional)
            }
        };

    private static Cbc DocumentoIdentidad(TipoDocumentoIdentidad tipo, string numero) => new(numero)
    {
        SchemeId = tipo.Codigo(), SchemeName = "Documento de Identidad", SchemeAgencyName = UblNs.Agency, SchemeUri = UblNs.Catalogo("06")
    };

    private static Cbc Cantidad(decimal q, string unitCode) => new(q.ToString("0.00##", Inv))
    { UnitCode = unitCode, UnitCodeListId = "UN/ECE rec 20", UnitCodeListAgencyName = UneceAgency };

    private static Cbc Importe(decimal v, string cur, bool hastaDiezDecimales = false)
        => new(hastaDiezDecimales ? v.ToString("0.00########", Inv) : v.ToString("F2", Inv)) { CurrencyId = cur };
}
