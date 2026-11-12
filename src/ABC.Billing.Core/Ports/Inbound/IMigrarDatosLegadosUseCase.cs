using ABC.Billing.Core.Application.Commands;

namespace ABC.Billing.Core.Ports.Inbound;

public interface IMigrarDatosLegadosUseCase
{
    /// <summary>Migra los 5 campos legados (fecha, ingreso, salida, producto, nro_documento) al kardex formal.</summary>
    Task<MigracionResultado> MigrarLoteAsync(IReadOnlyList<RegistroMovimientoLegado> registros, CancellationToken ct = default);
}
