using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Presentation.Api.DTOs.Responses;

public sealed record TributoResponse(string Codigo, string Nombre, string CodigoInternacional, decimal? BaseImponible, decimal Monto);

public sealed record ItemResponse(int Numero, string Descripcion, string UnidadMedida, decimal Cantidad, decimal ValorUnitario,
    string AfectacionIgv, decimal ValorVenta, decimal Isc, decimal Igv, decimal Icbper, decimal PrecioUnitarioConImpuestos);

public sealed record CdrResponse(string? Codigo, string? Descripcion, IReadOnlyList<string> Observaciones);

public sealed record TotalesResponse(decimal Gravadas, decimal Exoneradas, decimal Inafectas, decimal Exportacion, decimal Gratuitas,
    decimal BaseImponibleIgv, decimal Igv, decimal Isc, decimal Icbper, decimal ValorVenta, decimal PrecioVenta, decimal ImporteTotal);

public sealed record InvoiceResponse(Guid Id, string TipoComprobante, string SerieNumero, string NombreArchivo, DateTime FechaEmision,
    string Moneda, string ClienteDocumento, string ClienteRazonSocial, TotalesResponse Totales,
    IReadOnlyList<TributoResponse> Tributos, IReadOnlyList<ItemResponse> Items, string Estado, string? DigestValue, CdrResponse? Cdr)
{
    public static InvoiceResponse From(Comprobante i) => new(
        i.Id, i.TipoComprobante.Codigo(), i.SerieNumero, i.NombreArchivo, i.FechaEmision, i.Moneda.ToString(),
        $"{i.Cliente.TipoDocumento.Codigo()}-{i.Cliente.NumeroDocumento}", i.Cliente.RazonSocial,
        new TotalesResponse(i.TotalOperacionesGravadas, i.TotalOperacionesExoneradas, i.TotalOperacionesInafectas, i.TotalExportacion,
            i.TotalOperacionesGratuitas, i.BaseImponibleIgv, i.TotalIgv, i.TotalIsc, i.TotalIcbper, i.TotalValorVenta, i.TotalPrecioVenta, i.ImporteTotalPagar),
        i.Tributos.Select(t => new TributoResponse(t.Tributo.Codigo, t.Tributo.Nombre, t.Tributo.CodigoInternacional, t.BaseImponible, t.Monto)).ToList(),
        i.Lineas.Select(l => new ItemResponse(l.NumeroLinea, l.Descripcion, l.UnidadMedida.ToString(), l.Cantidad, l.ValorUnitario,
            l.AfectacionIgv.Codigo(), l.ValorVenta, l.MontoIsc, l.MontoIgv, l.MontoIcbper, l.PrecioUnitarioConImpuestos)).ToList(),
        i.Estado.ToString(), i.DigestValue,
        i.CdrRespuestaCodigo is null ? null : new CdrResponse(i.CdrRespuestaCodigo, i.CdrDescripcion, i.CdrObservaciones));
}

public sealed record TransmisionResponse(string Estado, int? CodigoRespuesta, string Descripcion, IReadOnlyList<string> Observaciones)
{
    public static TransmisionResponse From(TransmisionResultado r) => new(r.Estado.ToString(), r.CodigoRespuesta, r.Descripcion, r.Observaciones);
}
