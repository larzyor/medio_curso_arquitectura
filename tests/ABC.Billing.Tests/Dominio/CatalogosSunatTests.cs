using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Dominio;

public class CatalogosSunatTests
{
    [Fact]
    public void El_catalogo_09_tiene_13_codigos_del_01_al_13()
    {
        var codigos = Enum.GetValues<TipoNotaCredito>().Select(t => (int)t).Order().ToArray();
        Assert.Equal(13, codigos.Length);
        Assert.Equal(Enumerable.Range(1, 13), codigos);
    }

    [Theory]
    [InlineData(TipoNotaCredito.AjustesDeOperacionesDeExportacion, 11)]
    [InlineData(TipoNotaCredito.AjustesAfectosAlIvap, 12)]
    [InlineData(TipoNotaCredito.CorreccionDelMontoNetoPendienteOFechasDeVencimiento, 13)]
    public void Los_tres_codigos_nuevos_del_catalogo_09_existen(TipoNotaCredito tipo, int codigo) =>
        Assert.Equal(codigo, (int)tipo);

    [Fact]
    public void El_catalogo_07_tiene_19_codigos_de_afectacion() =>
        Assert.Equal(19, Enum.GetValues<AfectacionIgv>().Length);

    [Fact]
    public void El_catalogo_14_usa_los_codigos_1001_a_1004_y_3001()
    {
        Assert.Equal(1001, (int)CodigoTotalOperacion.Gravadas);
        Assert.Equal(1002, (int)CodigoTotalOperacion.Inafectas);
        Assert.Equal(1003, (int)CodigoTotalOperacion.Exoneradas);
        Assert.Equal(1004, (int)CodigoTotalOperacion.Gratuitas);
        Assert.Equal(3001, (int)CodigoTotalOperacion.Fise);
    }

    [Fact]
    public void Los_totales_por_tipo_de_operacion_coinciden_con_el_caso_completo()
    {
        var t = TotalesPorTipoOperacion.De(Fabrica.ComprobanteCompleto());
        Assert.Equal(210.30m, t[CodigoTotalOperacion.Gravadas]);
        Assert.Equal(8.00m, t[CodigoTotalOperacion.Inafectas]);
        Assert.Equal(60.00m, t[CodigoTotalOperacion.Exoneradas]);
        Assert.Equal(20.00m, t[CodigoTotalOperacion.Gratuitas]);
    }
}
