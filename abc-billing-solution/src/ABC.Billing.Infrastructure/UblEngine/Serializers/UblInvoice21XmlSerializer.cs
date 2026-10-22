using System.Text;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.UblEngine.Serializers;

/// <summary>
/// Adaptador que serializa la entidad Invoice a un documento XML conforme a UBL-Invoice-2.1.xsd.
/// Resuelve la equivalencia técnica:
///   SUNAT (Perú) 'serie_numero' -> UBL 2.1 '<cbc:ID>'
/// </summary>
public class UblInvoice21XmlSerializer : IUblXmlGenerator
{
    public string GenerarXmlUbl21(Invoice invoice)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>");
        sb.AppendLine("<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"");
        sb.AppendLine("         xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"");
        sb.AppendLine("         xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\"");
        sb.AppendLine("         xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\">");
        
        // Espacio para la firma digital
        sb.AppendLine("  <ext:UBLExtensions>");
        sb.AppendLine("    <ext:UBLExtension>");
        sb.AppendLine("      <ext:ExtensionContent />");
        sb.AppendLine("    </ext:UBLExtension>");
        sb.AppendLine("  </ext:UBLExtensions>");

        sb.AppendLine("  <cbc:UBLVersionID>2.1</cbc:UBLVersionID>");
        sb.AppendLine("  <cbc:CustomizationID>2.0</cbc:CustomizationID>");

        // MAPEO CLAVE EXPLICADO EN EL EXAMEN:
        // En Perú se le llama 'serie_numero'; en UBL 2.1 es <cbc:ID>
        sb.AppendLine($"  <cbc:ID>{invoice.SerieNumero}</cbc:ID>");
        sb.AppendLine($"  <cbc:IssueDate>{invoice.FechaEmision:yyyy-MM-dd}</cbc:IssueDate>");
        sb.AppendLine($"  <cbc:IssueTime>{invoice.FechaEmision:HH:mm:ss}</cbc:IssueTime>");
        sb.AppendLine($"  <cbc:InvoiceTypeCode listID=\"0101\">{(int)invoice.TipoComprobante:D2}</cbc:InvoiceTypeCode>");
        sb.AppendLine($"  <cbc:DocumentCurrencyCode>{invoice.Moneda}</cbc:DocumentCurrencyCode>");

        // Datos del Receptor (Cliente)
        sb.AppendLine("  <cac:AccountingCustomerParty>");
        sb.AppendLine("    <cac:Party>");
        sb.AppendLine("      <cac:PartyIdentification>");
        sb.AppendLine($"        <cbc:ID schemeID=\"{(int)invoice.Cliente.TipoDocumento}\">{invoice.Cliente.NumeroDocumento}</cbc:ID>");
        sb.AppendLine("      </cac:PartyIdentification>");
        sb.AppendLine("      <cac:PartyLegalEntity>");
        sb.AppendLine($"        <cbc:RegistrationName><![CDATA[{invoice.Cliente.RazonSocial}]]></cbc:RegistrationName>");
        sb.AppendLine("      </cac:PartyLegalEntity>");
        sb.AppendLine("    </cac:Party>");
        sb.AppendLine("  </cac:AccountingCustomerParty>");

        // Resumen de Tributos (Catálogo 08: 1000 IGV, 7152 ICBPER)
        foreach (var tributo in invoice.Tributos)
        {
            sb.AppendLine("  <cac:TaxTotal>");
            sb.AppendLine($"    <cbc:TaxAmount currencyID=\"{invoice.Moneda}\">{tributo.MontoTributo:F2}</cbc:TaxAmount>");
            sb.AppendLine("    <cac:TaxSubtotal>");
            sb.AppendLine($"      <cbc:TaxableAmount currencyID=\"{invoice.Moneda}\">{tributo.BaseImponible:F2}</cbc:TaxableAmount>");
            sb.AppendLine($"      <cbc:TaxAmount currencyID=\"{invoice.Moneda}\">{tributo.MontoTributo:F2}</cbc:TaxAmount>");
            sb.AppendLine("      <cac:TaxCategory>");
            sb.AppendLine("        <cac:TaxScheme>");
            sb.AppendLine($"          <cbc:ID>{tributo.CodigoTributo}</cbc:ID>"); // Catálogo 08
            sb.AppendLine($"          <cbc:Name>{tributo.NombreTributo}</cbc:Name>");
            sb.AppendLine($"          <cbc:TaxTypeCode>{tributo.CodigoInternacional}</cbc:TaxTypeCode>");
            sb.AppendLine("        </cac:TaxScheme>");
            sb.AppendLine("      </cac:TaxCategory>");
            sb.AppendLine("    </cac:TaxSubtotal>");
            sb.AppendLine("  </cac:TaxTotal>");
        }

        // Total a Pagar
        sb.AppendLine("  <cac:LegalMonetaryTotal>");
        sb.AppendLine($"    <cbc:LineExtensionAmount currencyID=\"{invoice.Moneda}\">{invoice.TotalOperacionesGravadas:F2}</cbc:LineExtensionAmount>");
        sb.AppendLine($"    <cbc:PayableAmount currencyID=\"{invoice.Moneda}\">{invoice.ImporteTotalPagar:F2}</cbc:PayableAmount>");
        sb.AppendLine("  </cac:LegalMonetaryTotal>");

        sb.AppendLine("</Invoice>");
        return sb.ToString();
    }
}
