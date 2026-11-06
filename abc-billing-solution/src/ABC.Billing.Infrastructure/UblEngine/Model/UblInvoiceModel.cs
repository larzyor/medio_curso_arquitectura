using System.Xml.Serialization;

namespace ABC.Billing.Infrastructure.UblEngine.Model;

// Clases POCO que reflejan el subconjunto de UBL-Invoice-2.1.xsd que usa SUNAT. El ORDEN de los elementos
// (Order) sigue la secuencia del XSD. Si se prefiere, pueden reemplazarse por las clases generadas con
// XmlSchemaClassGenerator a partir del propio XSD; el serializador solo depende de estas propiedades.

/// <summary>Elemento cbc:* de texto/código/importe con sus atributos opcionales (los nulos no se serializan).</summary>
public sealed class Cbc
{
    [XmlAttribute("currencyID")] public string? CurrencyId { get; set; }
    [XmlAttribute("unitCode")] public string? UnitCode { get; set; }
    [XmlAttribute("unitCodeListID")] public string? UnitCodeListId { get; set; }
    [XmlAttribute("unitCodeListAgencyName")] public string? UnitCodeListAgencyName { get; set; }
    [XmlAttribute("listID")] public string? ListId { get; set; }
    [XmlAttribute("listName")] public string? ListName { get; set; }
    [XmlAttribute("listAgencyName")] public string? ListAgencyName { get; set; }
    [XmlAttribute("listURI")] public string? ListUri { get; set; }
    [XmlAttribute("schemeID")] public string? SchemeId { get; set; }
    [XmlAttribute("schemeName")] public string? SchemeName { get; set; }
    [XmlAttribute("schemeAgencyName")] public string? SchemeAgencyName { get; set; }
    [XmlAttribute("schemeURI")] public string? SchemeUri { get; set; }
    [XmlAttribute("languageLocaleID")] public string? LanguageLocaleId { get; set; }
    [XmlText] public string Value { get; set; } = "";

    public Cbc() { }
    public Cbc(string value) { Value = value; }
}

public sealed class UblExtensions
{
    [XmlElement("UBLExtension", Namespace = UblNs.Ext)] public List<UblExtension> Items { get; set; } = [];
}

public sealed class UblExtension
{
    [XmlElement("ExtensionContent", Namespace = UblNs.Ext)] public ExtensionContent Content { get; set; } = new();
}

/// <summary>Vacío al generar; el firmador inserta aquí el nodo ds:Signature (XMLDSig).</summary>
public sealed class ExtensionContent;

public sealed class UblSignature
{
    [XmlElement("ID", Namespace = UblNs.Cbc, Order = 1)] public Cbc Id { get; set; } = new();
    [XmlElement("SignatoryParty", Namespace = UblNs.Cac, Order = 2)] public UblParty SignatoryParty { get; set; } = new();
    [XmlElement("DigitalSignatureAttachment", Namespace = UblNs.Cac, Order = 3)] public UblDigitalSignatureAttachment Attachment { get; set; } = new();
}

public sealed class UblDigitalSignatureAttachment
{
    [XmlElement("ExternalReference", Namespace = UblNs.Cac)] public UblExternalReference ExternalReference { get; set; } = new();
}

public sealed class UblExternalReference
{
    [XmlElement("URI", Namespace = UblNs.Cbc)] public Cbc Uri { get; set; } = new();
}

public sealed class UblAccountingParty
{
    [XmlElement("Party", Namespace = UblNs.Cac)] public UblParty Party { get; set; } = new();
}

public sealed class UblParty
{
    [XmlElement("PartyIdentification", Namespace = UblNs.Cac, Order = 1)] public UblPartyIdentification? PartyIdentification { get; set; }
    [XmlElement("PartyName", Namespace = UblNs.Cac, Order = 2)] public UblPartyName? PartyName { get; set; }
    [XmlElement("PartyTaxScheme", Namespace = UblNs.Cac, Order = 3)] public UblPartyTaxScheme? PartyTaxScheme { get; set; }
}

public sealed class UblPartyIdentification
{
    [XmlElement("ID", Namespace = UblNs.Cbc)] public Cbc Id { get; set; } = new();
}

public sealed class UblPartyName
{
    [XmlElement("Name", Namespace = UblNs.Cbc)] public Cbc Name { get; set; } = new();
}

public sealed class UblPartyTaxScheme
{
    [XmlElement("RegistrationName", Namespace = UblNs.Cbc, Order = 1)] public Cbc RegistrationName { get; set; } = new();
    [XmlElement("CompanyID", Namespace = UblNs.Cbc, Order = 2)] public Cbc CompanyId { get; set; } = new();
    [XmlElement("RegistrationAddress", Namespace = UblNs.Cac, Order = 3)] public UblRegistrationAddress? RegistrationAddress { get; set; }
    [XmlElement("TaxScheme", Namespace = UblNs.Cac, Order = 4)] public UblTaxSchemeRef TaxScheme { get; set; } = new();
}

public sealed class UblRegistrationAddress
{
    [XmlElement("AddressTypeCode", Namespace = UblNs.Cbc)] public Cbc AddressTypeCode { get; set; } = new();
}

/// <summary>cac:TaxScheme dentro de PartyTaxScheme: solo cbc:ID ("-" cuando no aplica).</summary>
public sealed class UblTaxSchemeRef
{
    [XmlElement("ID", Namespace = UblNs.Cbc)] public Cbc Id { get; set; } = new("-");
}

public sealed class UblTaxTotal
{
    [XmlElement("TaxAmount", Namespace = UblNs.Cbc, Order = 1)] public Cbc TaxAmount { get; set; } = new();
    [XmlElement("TaxSubtotal", Namespace = UblNs.Cac, Order = 2)] public List<UblTaxSubtotal> Subtotals { get; set; } = [];
}

public sealed class UblTaxSubtotal
{
    [XmlElement("TaxableAmount", Namespace = UblNs.Cbc, Order = 1)] public Cbc? TaxableAmount { get; set; }
    [XmlElement("TaxAmount", Namespace = UblNs.Cbc, Order = 2)] public Cbc TaxAmount { get; set; } = new();
    [XmlElement("BaseUnitMeasure", Namespace = UblNs.Cbc, Order = 3)] public Cbc? BaseUnitMeasure { get; set; }
    [XmlElement("TaxCategory", Namespace = UblNs.Cac, Order = 4)] public UblTaxCategory TaxCategory { get; set; } = new();
}

public sealed class UblTaxCategory
{
    [XmlElement("ID", Namespace = UblNs.Cbc, Order = 1)] public Cbc Id { get; set; } = new();
    [XmlElement("Percent", Namespace = UblNs.Cbc, Order = 2)] public Cbc? Percent { get; set; }
    [XmlElement("PerUnitAmount", Namespace = UblNs.Cbc, Order = 3)] public Cbc? PerUnitAmount { get; set; }
    [XmlElement("TaxExemptionReasonCode", Namespace = UblNs.Cbc, Order = 4)] public Cbc? TaxExemptionReasonCode { get; set; }
    [XmlElement("TierRange", Namespace = UblNs.Cbc, Order = 5)] public Cbc? TierRange { get; set; }
    [XmlElement("TaxScheme", Namespace = UblNs.Cac, Order = 6)] public UblTaxScheme TaxScheme { get; set; } = new();
}

public sealed class UblTaxScheme
{
    [XmlElement("ID", Namespace = UblNs.Cbc, Order = 1)] public Cbc Id { get; set; } = new();
    [XmlElement("Name", Namespace = UblNs.Cbc, Order = 2)] public Cbc Name { get; set; } = new();
    [XmlElement("TaxTypeCode", Namespace = UblNs.Cbc, Order = 3)] public Cbc TaxTypeCode { get; set; } = new();
}

public sealed class UblLegalMonetaryTotal
{
    [XmlElement("LineExtensionAmount", Namespace = UblNs.Cbc, Order = 1)] public Cbc LineExtensionAmount { get; set; } = new();
    [XmlElement("TaxInclusiveAmount", Namespace = UblNs.Cbc, Order = 2)] public Cbc TaxInclusiveAmount { get; set; } = new();
    [XmlElement("PayableAmount", Namespace = UblNs.Cbc, Order = 3)] public Cbc PayableAmount { get; set; } = new();
}

public sealed class UblInvoiceLine
{
    [XmlElement("ID", Namespace = UblNs.Cbc, Order = 1)] public Cbc Id { get; set; } = new();
    [XmlElement("InvoicedQuantity", Namespace = UblNs.Cbc, Order = 2)] public Cbc InvoicedQuantity { get; set; } = new();
    [XmlElement("LineExtensionAmount", Namespace = UblNs.Cbc, Order = 3)] public Cbc LineExtensionAmount { get; set; } = new();
    [XmlElement("PricingReference", Namespace = UblNs.Cac, Order = 4)] public UblPricingReference PricingReference { get; set; } = new();
    [XmlElement("TaxTotal", Namespace = UblNs.Cac, Order = 5)] public UblTaxTotal TaxTotal { get; set; } = new();
    [XmlElement("Item", Namespace = UblNs.Cac, Order = 6)] public UblItem Item { get; set; } = new();
    [XmlElement("Price", Namespace = UblNs.Cac, Order = 7)] public UblPrice Price { get; set; } = new();
}

public sealed class UblPricingReference
{
    [XmlElement("AlternativeConditionPrice", Namespace = UblNs.Cac)] public UblAlternativeConditionPrice Price { get; set; } = new();
}

public sealed class UblAlternativeConditionPrice
{
    [XmlElement("PriceAmount", Namespace = UblNs.Cbc, Order = 1)] public Cbc PriceAmount { get; set; } = new();
    [XmlElement("PriceTypeCode", Namespace = UblNs.Cbc, Order = 2)] public Cbc PriceTypeCode { get; set; } = new();
}

public sealed class UblItem
{
    [XmlElement("Description", Namespace = UblNs.Cbc, Order = 1)] public Cbc Description { get; set; } = new();
    [XmlElement("SellersItemIdentification", Namespace = UblNs.Cac, Order = 2)] public UblSellersItemIdentification? SellersItemIdentification { get; set; }
}

public sealed class UblSellersItemIdentification
{
    [XmlElement("ID", Namespace = UblNs.Cbc)] public Cbc Id { get; set; } = new();
}

public sealed class UblPrice
{
    [XmlElement("PriceAmount", Namespace = UblNs.Cbc)] public Cbc PriceAmount { get; set; } = new();
}

[XmlRoot("Invoice", Namespace = UblNs.Invoice)]
public sealed class UblInvoice
{
    [XmlElement("UBLExtensions", Namespace = UblNs.Ext, Order = 1)] public UblExtensions Extensions { get; set; } = new();
    [XmlElement("UBLVersionID", Namespace = UblNs.Cbc, Order = 2)] public Cbc UblVersionId { get; set; } = new("2.1");
    [XmlElement("CustomizationID", Namespace = UblNs.Cbc, Order = 3)] public Cbc CustomizationId { get; set; } = new("2.0");
    [XmlElement("ProfileID", Namespace = UblNs.Cbc, Order = 4)] public Cbc ProfileId { get; set; } = new();
    [XmlElement("ID", Namespace = UblNs.Cbc, Order = 5)] public Cbc Id { get; set; } = new();
    [XmlElement("IssueDate", Namespace = UblNs.Cbc, Order = 6)] public Cbc IssueDate { get; set; } = new();
    [XmlElement("IssueTime", Namespace = UblNs.Cbc, Order = 7)] public Cbc IssueTime { get; set; } = new();
    [XmlElement("InvoiceTypeCode", Namespace = UblNs.Cbc, Order = 8)] public Cbc InvoiceTypeCode { get; set; } = new();
    [XmlElement("Note", Namespace = UblNs.Cbc, Order = 9)] public List<Cbc> Notes { get; set; } = [];
    [XmlElement("DocumentCurrencyCode", Namespace = UblNs.Cbc, Order = 10)] public Cbc DocumentCurrencyCode { get; set; } = new();
    [XmlElement("Signature", Namespace = UblNs.Cac, Order = 11)] public UblSignature Signature { get; set; } = new();
    [XmlElement("AccountingSupplierParty", Namespace = UblNs.Cac, Order = 12)] public UblAccountingParty Supplier { get; set; } = new();
    [XmlElement("AccountingCustomerParty", Namespace = UblNs.Cac, Order = 13)] public UblAccountingParty Customer { get; set; } = new();
    [XmlElement("TaxTotal", Namespace = UblNs.Cac, Order = 14)] public UblTaxTotal TaxTotal { get; set; } = new();
    [XmlElement("LegalMonetaryTotal", Namespace = UblNs.Cac, Order = 15)] public UblLegalMonetaryTotal LegalMonetaryTotal { get; set; } = new();
    [XmlElement("InvoiceLine", Namespace = UblNs.Cac, Order = 16)] public List<UblInvoiceLine> Lines { get; set; } = [];
}
