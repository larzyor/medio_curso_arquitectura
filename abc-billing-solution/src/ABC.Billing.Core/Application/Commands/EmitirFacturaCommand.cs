using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Application.Commands;

/// <summary>Comando de entrada del caso de uso. Ya viene con catálogos tipados: los traduce la capa de presentación.</summary>
public sealed record EmitirFacturaCommand(
    TipoComprobante TipoComprobante,
    TipoOperacion TipoOperacion,
    string? SerieSolicitada,
    TipoMoneda Moneda,
    ClienteCommand Cliente,
    IReadOnlyList<ItemCommand> Items);

public sealed record ClienteCommand(TipoDocumentoIdentidad TipoDocumento, string NumeroDocumento, string RazonSocial, string? Direccion);

public sealed record ItemCommand(
    string CodigoProducto, string Descripcion, UnidadMedida UnidadMedida, decimal Cantidad, decimal ValorUnitario,
    AfectacionIgv AfectacionIgv, SistemaIsc? SistemaIsc = null, decimal TasaIsc = 0m, decimal MontoFijoIsc = 0m,
    bool AplicaIcbper = false);

public sealed record SeriesPorDefecto(string Factura = "F001", string Boleta = "B001");

public sealed record TransmisionResultado(
    EstadoComprobante Estado, int? CodigoRespuesta, string Descripcion, IReadOnlyList<string> Observaciones);

public sealed record LegacyMovementRecord(string Fecha, decimal Ingreso, decimal Salida, string Producto, string NroDocumento);

public sealed record SaldoProducto(string Sku, string Producto, decimal Saldo);
public sealed record ErrorMigracion(int Fila, string Mensaje);
public sealed record MigracionResultado(
    int TotalRegistros, int RegistrosMigrados, int RegistrosConError,
    IReadOnlyList<SaldoProducto> Saldos, IReadOnlyList<ErrorMigracion> Errores);
