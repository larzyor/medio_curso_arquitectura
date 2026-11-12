using System.Reflection;
using System.Xml.Schema;
using System.Xml.Serialization;
using ABC.Billing.Infrastructure.UblEngine.Generado;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Generacion;

/// <summary>Demuestra que las clases con las que se genera el XML SALEN del XSD y no de una copia hecha a mano.</summary>
public class GeneracionDesdeXsdTests
{
    private const string NsInvoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

    private static List<(string Nombre, string Espacio, bool Obligatorio, bool Repetible)> ElementosDelXsd(string nombreTipo, string espacioTipo)
    {
        var tipo = (XmlSchemaComplexType)Fabrica.Validador.Esquemas.GlobalTypes[new System.Xml.XmlQualifiedName(nombreTipo, espacioTipo)]!;
        var lista = new List<(string, string, bool, bool)>();
        void Recorrer(XmlSchemaParticle? p)
        {
            switch (p)
            {
                case XmlSchemaSequence s: foreach (var h in s.Items.OfType<XmlSchemaParticle>()) Recorrer(h); break;
                case XmlSchemaElement e: lista.Add((e.QualifiedName.Name, e.QualifiedName.Namespace, e.MinOccurs > 0, e.MaxOccurs > 1)); break;
            }
        }
        Recorrer(tipo.ContentTypeParticle);
        return lista;
    }

    private static List<(string Nombre, string Espacio, int Orden, PropertyInfo Propiedad)> ElementosDeLaClase(Type clase)
        => clase.GetProperties()
            .Select(p => (Attr: p.GetCustomAttribute<XmlElementAttribute>(), P: p))
            .Where(x => x.Attr != null)
            .Select(x => (x.Attr!.ElementName, x.Attr.Namespace ?? "", x.Attr.Order, x.P))
            .OrderBy(x => x.Order).ToList();

    [Fact]
    public void InvoiceType_tiene_los_mismos_elementos_y_en_el_mismo_orden_que_el_XSD()
    {
        var xsd = ElementosDelXsd("InvoiceType", NsInvoice).Select(e => (e.Nombre, e.Espacio)).ToList();
        var clase = ElementosDeLaClase(typeof(InvoiceType)).Select(e => (e.Nombre, e.Espacio)).ToList();
        Assert.True(xsd.Count > 40, "el tipo Invoice de UBL tiene decenas de elementos");
        Assert.Equal(xsd, clase);
    }

    [Theory]
    [InlineData("InvoiceLineType")]
    [InlineData("TaxTotalType")]
    [InlineData("TaxSubtotalType")]
    [InlineData("TaxCategoryType")]
    [InlineData("PartyType")]
    [InlineData("MonetaryTotalType")]
    public void Los_tipos_de_agregado_siguen_el_XSD_oficial(string tipo)
    {
        var xsd = ElementosDelXsd(tipo, "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2").Select(e => (e.Nombre, e.Espacio)).ToList();
        var clase = ElementosDeLaClase(typeof(InvoiceType).Assembly.GetType($"ABC.Billing.Infrastructure.UblEngine.Generado.{tipo}")!).Select(e => (e.Nombre, e.Espacio)).ToList();
        Assert.Equal(xsd, clase);
    }

    [Fact]
    public void Los_elementos_obligatorios_del_XSD_no_son_anulables_y_los_repetibles_son_listas()
    {
        var xsd = ElementosDelXsd("InvoiceType", NsInvoice);
        var clase = ElementosDeLaClase(typeof(InvoiceType));
        foreach (var e in xsd)
        {
            var prop = clase.Single(c => c.Nombre == e.Nombre).Propiedad;
            if (e.Repetible) Assert.True(prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>), $"{e.Nombre} debe ser List<>");
            else if (e.Obligatorio) Assert.False(new NullabilityInfoContext().Create(prop).WriteState == NullabilityState.Nullable, $"{e.Nombre} es obligatorio en el XSD");
        }
    }

    [Fact]
    public void Cada_importe_generado_tiene_el_atributo_currencyID_del_XSD()
    {
        var atributos = typeof(TaxAmountType).GetProperties().Select(p => p.GetCustomAttribute<XmlAttributeAttribute>()?.AttributeName).Where(a => a != null).ToList();
        Assert.Contains("currencyID", atributos);
        Assert.NotNull(typeof(TaxAmountType).GetProperty("Value")!.GetCustomAttribute<XmlTextAttribute>());
        Assert.Equal(typeof(decimal), typeof(TaxAmountType).GetProperty("Value")!.PropertyType);
    }

    [Fact]
    public void La_raiz_generada_se_llama_Invoice_en_el_espacio_de_nombres_de_UBL()
    {
        var raiz = typeof(InvoiceType).GetCustomAttribute<XmlRootAttribute>()!;
        Assert.Equal("Invoice", raiz.ElementName);
        Assert.Equal(NsInvoice, raiz.Namespace);
    }

    [Fact]
    public void El_codigo_generado_declara_su_origen_y_la_regla_de_no_editar()
    {
        var ruta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ABC.Billing.Infrastructure", "UblEngine", "Generado", "UblInvoice21.generated.cs"));
        if (!File.Exists(ruta)) return;   // al ejecutar fuera del repositorio no se comprueba el archivo fuente
        var cabecera = string.Join('\n', File.ReadLines(ruta).Take(3));
        Assert.Contains("UBL-Invoice-2.1.xsd", cabecera);
        Assert.Contains("NO EDITAR", cabecera);
    }
}
