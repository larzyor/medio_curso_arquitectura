using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using ABC.Billing.Infrastructure.UblEngine.Generated;
using ABC.Billing.Infrastructure.UblEngine.Serializers;

namespace ABC.Billing.UblEngine.Tests;

/// <summary>
/// El modelo de UblEngine/Generated se generó desde UBL-Invoice-2.1.xsd. Estas pruebas leen el XSD oficial y
/// comprueban que las clases siguen diciendo lo mismo que el esquema: si alguien edita el .g.cs a mano o cambia
/// el XSD sin regenerar, fallan.
/// </summary>
public class ModeloGeneradoDesdeXsdTests
{
    private static readonly XmlSchemaSet Esquemas = new UblXsdValidator().Esquemas;

    private static readonly List<Type> ClasesGeneradas = typeof(InvoiceType).Assembly.GetTypes()
        .Where(t => t.Namespace == typeof(InvoiceType).Namespace && t.GetCustomAttribute<XmlTypeAttribute>() is not null)
        .ToList();

    public static IEnumerable<object[]> Agregados() => ClasesGeneradas
        .Where(t => t.GetCustomAttribute<XmlTypeAttribute>()!.Namespace != UblXmlNs.Udt)
        .Select(t => new object[] { t.Name });

    [Fact]
    public void La_raiz_es_el_elemento_Invoice_del_namespace_UBL()
    {
        var raiz = typeof(InvoiceType).GetCustomAttribute<XmlRootAttribute>()!;
        Assert.Equal("Invoice", raiz.ElementName);
        Assert.Equal(UblXmlNs.Invoice, raiz.Namespace);
        Assert.NotNull(Esquemas.GlobalElements[new XmlQualifiedName("Invoice", UblXmlNs.Invoice)]);
    }

    [Fact]
    public void Se_genero_una_clase_por_cada_agregado_alcanzable_desde_Invoice()
    {
        var nombres = ClasesGeneradas.Select(t => t.Name).ToHashSet();
        Assert.Subset(nombres, new HashSet<string>
        {
            "InvoiceType", "InvoiceLineType", "TaxTotalType", "TaxSubtotalType", "TaxCategoryType", "TaxSchemeType",
            "PartyType", "SupplierPartyType", "CustomerPartyType", "MonetaryTotalType", "SignatureType", "UBLExtensionType",
            "AmountType", "CodeType", "IdentifierType", "QuantityType", "TextType"
        });
        Assert.True(ClasesGeneradas.Count > 100, $"Solo hay {ClasesGeneradas.Count} clases generadas");
    }

    [Theory]
    [MemberData(nameof(Agregados))]
    public void Cada_clase_tiene_los_elementos_del_xsd_sequence_en_el_mismo_orden_y_cardinalidad(string clase)
    {
        var tipo = ClasesGeneradas.Single(t => t.Name == clase);
        var xmlType = tipo.GetCustomAttribute<XmlTypeAttribute>()!;
        var complejo = Assert.IsType<XmlSchemaComplexType>(Esquemas.GlobalTypes[new XmlQualifiedName(xmlType.TypeName, xmlType.Namespace)]);
        // Particle = la secuencia tal como está escrita en el XSD (ContentTypeParticle puede venir simplificada).
        var secuencia = Assert.IsType<XmlSchemaSequence>(complejo.Particle);

        var segunXsd = secuencia.Items.OfType<XmlSchemaElement>()
            .Select(e => (Nombre: e.RefName.IsEmpty ? e.QualifiedName : e.RefName, e.MaxOccurs))
            .Select(e => $"{e.Nombre.Namespace}#{e.Nombre.Name}{(e.MaxOccurs > 1 ? "[*]" : "")}")
            .ToList();

        var segunClase = tipo.GetProperties()
            .Select(p => (Propiedad: p, Elemento: p.GetCustomAttribute<XmlElementAttribute>()))
            .Where(x => x.Elemento is not null)
            .OrderBy(x => x.Elemento!.Order)
            .Select(x => $"{x.Elemento!.Namespace}#{x.Elemento.ElementName}{(EsLista(x.Propiedad.PropertyType) ? "[*]" : "")}")
            .ToList();

        Assert.Equal(segunXsd, segunClase);
    }

    [Fact]
    public void Cada_elemento_cbc_usa_la_clase_del_tipo_de_dato_udt_del_que_deriva_en_el_xsd()
    {
        var revisados = 0;
        foreach (var tipo in ClasesGeneradas)
        foreach (var p in tipo.GetProperties())
        {
            var el = p.GetCustomAttribute<XmlElementAttribute>();
            if (el is null || el.Namespace != UblXmlNs.Cbc) continue;

            var declarado = (XmlSchemaElement)Esquemas.GlobalElements[new XmlQualifiedName(el.ElementName, UblXmlNs.Cbc)]!;
            var udt = declarado.ElementSchemaType;
            while (udt is not null && udt.QualifiedName.Namespace != UblXmlNs.Udt) udt = udt.BaseXmlSchemaType;

            var claseDeLaPropiedad = EsLista(p.PropertyType) ? p.PropertyType.GetGenericArguments()[0] : p.PropertyType;
            Assert.Equal($"udt:{udt?.QualifiedName.Name}", $"udt:{claseDeLaPropiedad.Name}");
            revisados++;
        }
        Assert.True(revisados > 500, $"Solo se revisaron {revisados} elementos cbc");
    }

    [Fact]
    public void Los_atributos_de_los_tipos_de_dato_son_los_del_xsd()
    {
        foreach (var nombre in new[] { "AmountType", "CodeType", "IdentifierType", "QuantityType", "TextType", "MeasureType" })
        {
            var tipoXsd = (XmlSchemaComplexType)Esquemas.GlobalTypes[new XmlQualifiedName(nombre, UblXmlNs.Udt)]!;
            var segunXsd = tipoXsd.AttributeUses.Values.Cast<XmlSchemaAttribute>()
                .Where(a => a.Use != XmlSchemaUse.Prohibited).Select(a => a.QualifiedName.Name).OrderBy(n => n);
            var segunClase = ClasesGeneradas.Single(t => t.Name == nombre).GetProperties()
                .Select(p => p.GetCustomAttribute<XmlAttributeAttribute>()?.AttributeName).OfType<string>().OrderBy(n => n);
            Assert.Equal(segunXsd, segunClase);
        }
    }

    [Fact]
    public void ExtensionContent_acepta_cualquier_elemento_para_alojar_la_firma()
    {
        var any = typeof(ExtensionContentType).GetProperty(nameof(ExtensionContentType.Any))!;
        Assert.NotNull(any.GetCustomAttribute<XmlAnyElementAttribute>());
        Assert.Equal(typeof(XmlElement), any.PropertyType);
    }

    private static bool EsLista(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
}
