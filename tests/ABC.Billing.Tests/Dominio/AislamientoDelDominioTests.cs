using System.Reflection;
using System.Xml.Serialization;
using ABC.Billing.Core.Domain.Entities;
using Xunit;

namespace ABC.Billing.Tests.Dominio;

/// <summary>"En el lenguaje del dominio": el núcleo habla de comprobantes, líneas, tributos y clientes; no de etiquetas XML.</summary>
public class AislamientoDelDominioTests
{
    private static readonly Assembly Core = typeof(Comprobante).Assembly;

    [Fact]
    public void El_nucleo_no_referencia_a_la_infraestructura_ni_a_ASP_NET()
    {
        var referencias = Core.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.DoesNotContain("ABC.Billing.Infrastructure", referencias);
        Assert.DoesNotContain(referencias, r => r.StartsWith("Microsoft.AspNetCore"));
        Assert.DoesNotContain("System.Security.Cryptography.Xml", referencias);
    }

    [Fact]
    public void Ninguna_clase_del_dominio_lleva_atributos_de_XML()
    {
        foreach (var tipo in Core.GetTypes())
        {
            Assert.Null(tipo.GetCustomAttribute<XmlRootAttribute>());
            foreach (var p in tipo.GetProperties())
            {
                Assert.Null(p.GetCustomAttribute<XmlElementAttribute>());
                Assert.Null(p.GetCustomAttribute<XmlAttributeAttribute>());
            }
        }
    }

    [Theory]
    [InlineData("Comprobante")]
    [InlineData("LineaComprobante")]
    [InlineData("SubtotalTributo")]
    [InlineData("Cliente")]
    [InlineData("Emisor")]
    [InlineData("SerieComprobante")]
    [InlineData("Ruc")]
    public void Los_conceptos_del_negocio_se_llaman_como_en_el_negocio(string nombre)
        => Assert.NotNull(Core.GetTypes().SingleOrDefault(t => t.Name == nombre));

    [Fact]
    public void Los_puertos_hablan_en_terminos_del_dominio()
    {
        var puertos = Core.GetTypes().Where(t => t.IsInterface && t.Namespace!.EndsWith("Ports.Outbound")).Select(t => t.Name).ToList();
        Assert.Contains("IGeneradorXmlUbl", puertos);
        Assert.Contains("IRepositorioComprobantes", puertos);
        Assert.Contains("IFirmadorDigital", puertos);
    }

    [Fact]
    public void El_generador_de_XML_recibe_un_Comprobante_y_no_etiquetas()
    {
        var metodo = typeof(ABC.Billing.Core.Ports.Outbound.IGeneradorXmlUbl).GetMethod("GenerarXmlUbl21")!;
        Assert.Equal(typeof(Comprobante), metodo.GetParameters().Single().ParameterType);
    }
}
