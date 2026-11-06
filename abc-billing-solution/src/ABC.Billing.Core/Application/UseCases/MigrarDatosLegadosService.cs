using System.Globalization;
using System.Text.RegularExpressions;
using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Core.Application.UseCases;

/// <summary>
/// Migra el software básico (fecha, ingreso, salida, producto, nro_documento) SIN perder trazabilidad:
/// 1) cada fila original se conserva en una tabla de auditoría enlazada al kardex;
/// 2) por producto se crea UN movimiento "SALDO_INICIAL_MIGRACION" con el saldo (formato 12.1: la primera tupla es el saldo inicial);
/// 3) los movimientos históricos NO se facturan.
/// </summary>
public sealed class MigrarDatosLegadosService(IKardexRepository kardex) : IMigrarDatosLegadosUseCase
{
    private static readonly string[] Formatos = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];

    public async Task<MigracionResultado> MigrarLoteAsync(IReadOnlyList<LegacyMovementRecord> registros, CancellationToken ct = default)
    {
        var errores = new List<ErrorMigracion>();
        var validos = new List<(int Fila, DateOnly Fecha, LegacyMovementRecord R, string Norm)>();

        for (int i = 0; i < registros.Count; i++)
        {
            var r = registros[i];
            int fila = i + 1;
            if (!DateOnly.TryParseExact((r.Fecha ?? "").Trim(), Formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                { errores.Add(new(fila, $"Fecha inválida '{r.Fecha}' (use dd/MM/yyyy o yyyy-MM-dd).")); continue; }
            if (r.Ingreso < 0 || r.Salida < 0)
                { errores.Add(new(fila, "Ingreso y salida no pueden ser negativos.")); continue; }
            if (r.Ingreso == 0 && r.Salida == 0)
                { errores.Add(new(fila, "El movimiento no tiene ingreso ni salida.")); continue; }
            var norm = Normalizar(r.Producto);
            if (norm.Length == 0)
                { errores.Add(new(fila, "El producto es obligatorio.")); continue; }
            validos.Add((fila, fecha, r, norm));
        }

        // Un SKU estable por producto normalizado, en orden de aparición.
        var skus = new Dictionary<string, string>();
        foreach (var v in validos)
            if (!skus.ContainsKey(v.Norm)) skus[v.Norm] = $"MIG-{skus.Count + 1:D6}";

        var movimientos = new List<KardexMovement>();
        var auditoria = new List<LegacyMovementAudit>();
        var saldos = new List<SaldoProducto>();
        var invalidas = new HashSet<int>();

        foreach (var grupo in validos.GroupBy(v => v.Norm))
        {
            decimal saldo = grupo.Sum(g => g.R.Ingreso - g.R.Salida);
            if (saldo < 0)
            {
                foreach (var g in grupo) { errores.Add(new(g.Fila, $"Saldo negativo ({saldo}) para '{grupo.Key}': concilie el stock antes de migrar.")); invalidas.Add(g.Fila); }
                continue;
            }
            var sku = skus[grupo.Key];
            var mov = new KardexMovement
            {
                Fecha = grupo.Max(g => g.Fecha), Sku = sku, DescripcionExistencia = grupo.Key, Entrada = saldo
            };
            movimientos.Add(mov);
            saldos.Add(new SaldoProducto(sku, grupo.Key, saldo));
            auditoria.AddRange(grupo.Select(g => new LegacyMovementAudit(
                g.Fila, g.Fecha, g.R.Ingreso, g.R.Salida, g.R.Producto, g.R.NroDocumento, sku, mov.Id)));
        }

        if (movimientos.Count > 0)
            await kardex.GuardarMigracionAsync(movimientos, auditoria, ct);

        int migrados = auditoria.Count;
        return new MigracionResultado(registros.Count, migrados, registros.Count - migrados,
            saldos, errores.OrderBy(e => e.Fila).ToList());
    }

    private static string Normalizar(string? producto)
        => Regex.Replace((producto ?? "").Trim(), @"\s+", " ").ToUpperInvariant();
}
