using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using Xunit;

namespace ABC.Billing.Tests.Dominio;

public class ValoresDelDominioTests
{
    [Theory]
    [InlineData("20600000013")]
    [InlineData("20123456786")]
    [InlineData("20100066603")]
    public void Un_RUC_con_digito_verificador_correcto_es_valido(string ruc) => Assert.True(Ruc.EsValido(ruc));

    [Theory]
    [InlineData("20600000014")]   // dígito verificador incorrecto
    [InlineData("30600000013")]   // prefijo inexistente
    [InlineData("2060000001")]    // 10 dígitos
    [InlineData("")]
    public void Un_RUC_incorrecto_se_rechaza(string ruc) => Assert.False(Ruc.EsValido(ruc));

    [Theory]
    [InlineData("F001")]
    [InlineData("B001")]
    [InlineData("FA01")]
    [InlineData("B0A1")]
    public void Series_validas(string serie) => Assert.True(SerieComprobante.EsSerieValida(serie));

    [Theory]
    [InlineData("|001")]   // el patrón [F|B] anterior aceptaba la barra
    [InlineData("X001")]
    [InlineData("F01")]
    [InlineData("f001x")]
    public void Series_invalidas(string serie) => Assert.False(SerieComprobante.EsSerieValida(serie));

    [Fact]
    public void La_serie_numero_es_el_cbc_ID_y_tiene_version_impresa()
    {
        var s = new SerieComprobante("F001", 1);
        Assert.Equal("F001-1", s.SerieNumero);
        Assert.Equal("F001-00000001", s.SerieNumeroImpreso);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(100000000L)]
    public void El_correlativo_va_de_1_a_99999999(long correlativo)
        => Assert.Throws<DominioValidationException>(() => new SerieComprobante("F001", correlativo));

    [Fact]
    public void La_serie_B_no_corresponde_a_una_factura()
    {
        Assert.False(new SerieComprobante("B001", 5).EsCompatibleCon(TipoComprobante.Factura));
        Assert.True(new SerieComprobante("B001", 5).EsCompatibleCon(TipoComprobante.BoletaDeVenta));
    }

    [Fact]
    public void Los_nombres_de_archivo_siguen_el_manual_de_SUNAT()
    {
        var ruc = new Ruc("20600000013");
        Assert.Equal("20600000013-01-F001-1", NombreArchivoSunat.Comprobante(ruc, TipoComprobante.Factura, new SerieComprobante("F001", 1)));
        Assert.Equal("20600000013-03-B001-25", NombreArchivoSunat.Comprobante(ruc, TipoComprobante.BoletaDeVenta, new SerieComprobante("B001", 25)));
        Assert.Equal("20600000013-RA-20261103-1", NombreArchivoSunat.ComunicacionBaja(ruc, new DateOnly(2026, 11, 3), 1));
        Assert.Equal("20600000013-RC-20261103-12", NombreArchivoSunat.ResumenDiario(ruc, new DateOnly(2026, 11, 3), 12));
    }

    [Fact]
    public void El_correlativo_de_resumenes_va_de_1_a_5_digitos()
        => Assert.Throws<DominioValidationException>(() => NombreArchivoSunat.ResumenDiario(new Ruc("20600000013"), new DateOnly(2026, 11, 3), 100000));

    [Theory]
    [InlineData("321.19", "SON: TRESCIENTOS VEINTIUNO Y 19/100 SOLES")]
    [InlineData("0.50", "SON: CERO Y 50/100 SOLES")]
    [InlineData("100.00", "SON: CIEN Y 00/100 SOLES")]
    [InlineData("1000.00", "SON: MIL Y 00/100 SOLES")]
    [InlineData("21000.00", "SON: VEINTIUN MIL Y 00/100 SOLES")]
    [InlineData("1000000.00", "SON: UN MILLON Y 00/100 SOLES")]
    [InlineData("2345.67", "SON: DOS MIL TRESCIENTOS CUARENTA Y CINCO Y 67/100 SOLES")]
    public void El_monto_en_letras_de_la_leyenda_1000(string monto, string esperado)
        => Assert.Equal(esperado, MontoEnLetras.Convertir(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture), TipoMoneda.PEN));

    [Theory]
    [InlineData(0, ResultadoSunat.Aceptado)]
    [InlineData(135, ResultadoSunat.ExcepcionSunat)]
    [InlineData(1033, ResultadoSunat.ExcepcionContribuyente)]
    [InlineData(2047, ResultadoSunat.Rechazo)]
    [InlineData(3999, ResultadoSunat.Rechazo)]
    [InlineData(4031, ResultadoSunat.Observacion)]
    public void Los_codigos_de_respuesta_se_clasifican_por_rango(int codigo, ResultadoSunat esperado)
        => Assert.Equal(esperado, ClasificadorCodigosSunat.Clasificar(codigo));

    [Fact]
    public void El_catalogo_05_y_el_catalogo_08_no_se_mezclan()
    {
        Assert.Equal("1000", TributoSunat.Igv.Codigo);
        Assert.Equal("7152", TributoSunat.Icbper.Codigo);
        Assert.Equal("01", SistemaIsc.AlValor.Codigo());     // Catálogo 08: solo sistemas de ISC
        Assert.Equal(3, Enum.GetValues<SistemaIsc>().Length);
    }
}
