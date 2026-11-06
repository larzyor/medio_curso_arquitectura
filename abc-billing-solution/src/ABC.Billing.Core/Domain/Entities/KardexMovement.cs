using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>
/// Movimiento del kardex formal. Se alinea con el Registro de Inventario Permanente en Unidades Físicas
/// (formato 12.1 del PLE): fecha del documento, tipo de operación, descripción de la existencia,
/// unidad de medida, entradas y salidas ("la primera tupla corresponde al saldo inicial").
/// </summary>
public sealed class KardexMovement
{
    public const string ConceptoSaldoInicialMigracion = "SALDO_INICIAL_MIGRACION";

    public Guid Id { get; init; } = Guid.NewGuid();
    public DateOnly Fecha { get; init; }
    public required string Sku { get; init; }
    public required string DescripcionExistencia { get; init; }
    public UnidadMedida UnidadMedida { get; init; } = UnidadMedida.NIU;
    public decimal Entrada { get; init; }
    public decimal Salida { get; init; }
    public string Concepto { get; init; } = ConceptoSaldoInicialMigracion;
}

/// <summary>Copia de auditoría de cada fila del sistema legado (los 5 campos originales) enlazada al kardex.</summary>
public sealed record LegacyMovementAudit(
    int Fila, DateOnly Fecha, decimal Ingreso, decimal Salida, string ProductoOriginal,
    string NroDocumento, string Sku, Guid KardexMovementId);
