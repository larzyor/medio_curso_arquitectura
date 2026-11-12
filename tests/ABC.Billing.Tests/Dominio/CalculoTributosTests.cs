using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Tests.Soporte;
using Xunit;

namespace ABC.Billing.Tests.Dominio;

public class CalculoTributosTests
{
    [Fact]
    public void El_caso_completo_de_la_especificacion_da_321_19()
    {
        var c = Fabrica.ComprobanteCompleto();
        Assert.Equal(210.30m, c.TotalOperacionesGravadas);
        Assert.Equal(3.00m, c.TotalIsc);
        Assert.Equal(213.30m, c.BaseImponibleIgv);
        Assert.Equal(38.39m, c.TotalIgv);
        Assert.Equal(60.00m, c.TotalOperacionesExoneradas);
        Assert.Equal(8.00m, c.TotalOperacionesInafectas);
        Assert.Equal(1.50m, c.TotalIcbper);
        Assert.Equal(20.00m, c.TotalOperacionesGratuitas);
        Assert.Equal(3.60m, c.TotalIgvGratuitas);
        Assert.Equal(278.30m, c.TotalValorVenta);
        Assert.Equal(42.89m, c.TotalTributos);
        Assert.Equal(321.19m, c.ImporteTotalPagar);
    }

    [Fact]
    public void El_IGV_total_es_el_18_por_ciento_de_gravadas_mas_ISC()
    {
        var c = Fabrica.ComprobanteCompleto();
        Assert.Equal(Math.Round(0.18m * (c.TotalOperacionesGravadas + c.TotalIsc), 2), c.TotalIgv);
    }

    [Fact]
    public void La_base_del_IGV_incluye_el_ISC()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Licor", 10m, 10m, isc: SistemaIsc.AlValor, tasaIsc: 20m)]);
        var linea = c.Lineas.Single();
        Assert.Equal(100.00m, linea.ValorVenta);
        Assert.Equal(20.00m, linea.MontoIsc);
        Assert.Equal(120.00m, linea.BaseIgv);
        Assert.Equal(21.60m, linea.MontoIgv);
        Assert.Equal(141.60m, c.ImporteTotalPagar);
    }

    [Fact]
    public void El_ISC_de_monto_fijo_se_calcula_por_unidad()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Cigarrillos", 4m, 10m, isc: SistemaIsc.MontoFijo, montoFijoIsc: 1.5m)]);
        Assert.Equal(6.00m, c.TotalIsc);
        Assert.Equal(8.28m, c.TotalIgv);       // 18 % de (40.00 + 6.00)
    }

    [Fact]
    public void El_ISC_sistema_03_se_rechaza_con_un_mensaje_claro()
    {
        var error = Assert.Throws<DominioValidationException>(() => Fabrica.Armar([Fabrica.Linea(1, "X", 1m, 10m, isc: SistemaIsc.PrecioVentaPublico)]));
        Assert.Contains("no soportado", error.Message);
    }

    [Fact]
    public void El_ICBPER_se_cobra_aparte_y_no_entra_en_la_base_del_IGV()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Bolsa", 10m, 0.10m, icbper: true)]);
        Assert.Equal(1.00m, c.BaseImponibleIgv);
        Assert.Equal(0.18m, c.TotalIgv);
        Assert.Equal(5.00m, c.TotalIcbper);       // 10 bolsas × 0.50
        Assert.Equal(6.18m, c.ImporteTotalPagar);
    }

    [Fact]
    public void El_ICBPER_exige_cantidad_entera()
        => Assert.Throws<DominioValidationException>(() => Fabrica.Armar([Fabrica.Linea(1, "Bolsa", 2.5m, 0.10m, icbper: true)]));

    [Theory]
    [InlineData(AfectacionIgv.ExoneradoOperacionOnerosa)]
    [InlineData(AfectacionIgv.InafectoOperacionOnerosa)]
    [InlineData(AfectacionIgv.Exportacion)]
    public void Exonerado_inafecto_y_exportacion_no_pagan_IGV_pero_suman_al_valor_de_venta(AfectacionIgv afectacion)
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Producto", 3m, 10m, afectacion)]);
        Assert.Equal(0m, c.TotalIgv);
        Assert.Equal(30.00m, c.TotalValorVenta);
        Assert.Equal(30.00m, c.ImporteTotalPagar);
    }

    [Theory]
    [InlineData(AfectacionIgv.GravadoRetiroPorPremio)]
    [InlineData(AfectacionIgv.GravadoBonificaciones)]
    public void El_retiro_gravado_informa_IGV_pero_no_se_cobra(AfectacionIgv afectacion)
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Obsequio", 1m, 100m, afectacion)]);
        Assert.Equal(18.00m, c.TotalIgvGratuitas);
        Assert.Equal(0m, c.TotalIgv);
        Assert.Equal(0m, c.ImporteTotalPagar);
        Assert.Equal(100.00m, c.TotalOperacionesGratuitas);
    }

    [Fact]
    public void El_IVAP_no_esta_soportado_en_esta_fase()
        => Assert.Throws<DominioValidationException>(() => Fabrica.Armar([Fabrica.Linea(1, "Arroz pilado", 1m, 10m, AfectacionIgv.GravadoIvap)]));

    [Theory]
    [InlineData("0", "10")]
    [InlineData("-1", "10")]
    [InlineData("1", "0")]
    public void Cantidad_y_valor_unitario_deben_ser_positivos(string cantidad, string valor)
        => Assert.Throws<DominioValidationException>(() => Fabrica.Armar([Fabrica.Linea(1, "X", decimal.Parse(cantidad), decimal.Parse(valor))]));

    [Fact]
    public void Un_comprobante_sin_items_se_rechaza()
        => Assert.Throws<DominioValidationException>(() => Fabrica.Armar([]));

    [Fact]
    public void Los_subtotales_siguen_el_orden_IGV_ISC_EXO_INA_GRA_ICBPER()
        => Assert.Equal(new[] { "1000", "2000", "9997", "9998", "9996", "7152" }, Fabrica.ComprobanteCompleto().Tributos.Select(t => t.Tributo.Codigo));

    [Fact]
    public void El_redondeo_es_a_dos_decimales_alejandose_de_cero()
    {
        var c = Fabrica.Armar([Fabrica.Linea(1, "Detalle", 1m, 0.025m)]);   // 0.025 -> 0.03
        Assert.Equal(0.03m, c.TotalValorVenta);
    }
}
