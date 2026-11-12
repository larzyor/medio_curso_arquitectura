using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Application.UseCases;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using Xunit;

namespace ABC.Billing.Tests.Aplicacion;

public class MigracionDeDatosLegadosTests
{
    private static readonly RegistroMovimientoLegado[] Lote =
    [
        new("01/03/2024", 100, 0, "  arroz   extra ", "NI-001"),
        new("2024-03-05", 0, 30, "ARROZ EXTRA", "GR-014"),
        new("07/03/2024", 50, 0, "Azúcar rubia", "NI-002"),
        new("31/02/2024", 5, 0, "Fideos", "NI-003"),      // fecha imposible
        new("08/03/2024", 0, 10, "Aceite", "GR-015"),      // sale sin haber entrado: saldo negativo
        new("09/03/2024", 0, 0, "Sal", "NI-004")           // sin movimiento
    ];

    [Fact]
    public async Task Migra_las_filas_validas_y_reporta_las_demas_por_numero_de_fila()
    {
        var repo = new RepositorioKardexEnMemoria();
        var r = await new MigrarDatosLegadosService(repo).MigrarLoteAsync(Lote);
        Assert.Equal(6, r.TotalRegistros);
        Assert.Equal(3, r.RegistrosMigrados);
        Assert.Equal(3, r.RegistrosConError);
        Assert.Equal(new[] { 4, 5, 6 }, r.Errores.Select(e => e.Fila).OrderBy(x => x));
    }

    [Fact]
    public async Task El_saldo_se_calcula_por_producto_con_nombres_normalizados()
    {
        var r = await new MigrarDatosLegadosService(new RepositorioKardexEnMemoria()).MigrarLoteAsync(Lote);
        Assert.Equal(70m, r.Saldos.Single(s => s.Producto == "ARROZ EXTRA").Saldo);   // 100 − 30
        Assert.Equal(50m, r.Saldos.Single(s => s.Producto == "AZÚCAR RUBIA").Saldo);
    }

    [Fact]
    public async Task Crea_un_movimiento_de_saldo_inicial_por_producto()
    {
        var repo = new RepositorioKardexEnMemoria();
        await new MigrarDatosLegadosService(repo).MigrarLoteAsync(Lote);
        Assert.Equal(2, repo.Kardex.Count);
        Assert.All(repo.Kardex, k => Assert.Equal(MovimientoKardex.ConceptoSaldoInicialMigracion, k.Concepto));
    }

    [Fact]
    public async Task Conserva_las_filas_originales_con_su_nro_documento_enlazadas_al_kardex()
    {
        var repo = new RepositorioKardexEnMemoria();
        await new MigrarDatosLegadosService(repo).MigrarLoteAsync(Lote);
        Assert.Equal(3, repo.Auditoria.Count);
        var traslado = repo.Auditoria.Single(a => a.NroDocumento == "GR-014");
        Assert.Equal(30m, traslado.Salida);
        Assert.Contains(repo.Kardex, k => k.Id == traslado.MovimientoKardexId && k.Sku == traslado.Sku);
    }

    [Fact]
    public async Task Un_lote_sin_filas_validas_no_guarda_nada()
    {
        var repo = new RepositorioKardexEnMemoria();
        var r = await new MigrarDatosLegadosService(repo).MigrarLoteAsync([new("99/99/9999", 1, 0, "X", "N")]);
        Assert.Equal(0, r.RegistrosMigrados);
        Assert.Empty(repo.Kardex);
    }

    [Fact]
    public async Task El_mismo_producto_recibe_siempre_el_mismo_SKU_dentro_del_lote()
    {
        var repo = new RepositorioKardexEnMemoria();
        await new MigrarDatosLegadosService(repo).MigrarLoteAsync(Lote);
        Assert.Equal(1, repo.Auditoria.Where(a => a.ProductoOriginal.Trim().ToUpperInvariant().Contains("ARROZ")).Select(a => a.Sku).Distinct().Count());
    }
}
