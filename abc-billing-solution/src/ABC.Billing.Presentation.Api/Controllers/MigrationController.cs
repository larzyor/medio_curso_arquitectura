using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Presentation.Api.DTOs.Responses;

namespace ABC.Billing.Presentation.Api.Controllers;

/// <summary>
/// Controlador MVC para la migración e ingesta de datos del software legado informal.
/// </summary>
public class MigrationController
{
    private readonly IMigrarDatosLegadosUseCase _migrarUseCase;

    public MigrationController(IMigrarDatosLegadosUseCase migrarUseCase)
    {
        _migrarUseCase = migrarUseCase;
    }

    /// <summary>
    /// POST /api/v1/migration/legacy-stock
    /// Ingesta lotes con los 5 campos legados (fecha, ingreso, salida, producto, nro_documento).
    /// </summary>
    public async Task<MigrationReportResponse> IngestLegacyBatchAsync(List<LegacyMovementRecord> records, CancellationToken ct = default)
    {
        int procesados = await _migrarUseCase.MigrarLoteAsync(records, ct);

        return new MigrationReportResponse
        {
            TotalRegistrosRecibidos = records.Count,
            RegistrosMigrados = procesados,
            RegistrosConError = records.Count - procesados,
            Mensaje = $"Proceso de migración culminado. {procesados} movimientos de almacén incorporados al inventario formal."
        };
    }
}
