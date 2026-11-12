using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Presentation.Api.DTOs.Requests;

namespace ABC.Billing.Presentation.Api.DTOs;

/// <summary>
/// ÚNICA responsabilidad: traducir DTO → comando, validando que cada código pertenezca a su catálogo.
/// Aquí NO hay cálculo de impuestos (corrección respecto de la versión anterior).
/// </summary>
public static class InvoiceRequestMapper
{
    public static EmitirFacturaCommand ToCommand(CreateInvoiceRequest r)
    {
        var tipo = PorCodigo<TipoComprobante>("Catálogo 01 (tipo de comprobante)", r.TipoComprobante);
        if (tipo is not (TipoComprobante.Factura or TipoComprobante.BoletaDeVenta))
            throw new DominioValidationException("Solo se emiten facturas ('01') y boletas ('03') en este endpoint.");

        return new EmitirFacturaCommand(
            tipo,
            PorCodigo<TipoOperacion>("Catálogo 51 (tipo de operación)", r.TipoOperacion),
            string.IsNullOrWhiteSpace(r.Serie) ? null : r.Serie,
            PorNombre<TipoMoneda>("Catálogo 02 (moneda)", r.Moneda),
            new ClienteCommand(PorCodigo<TipoDocumentoIdentidad>("Catálogo 06 (tipo de documento)", r.Cliente.TipoDocumento),
                r.Cliente.NumeroDocumento, r.Cliente.RazonSocial, r.Cliente.Direccion),
            r.Items.Select(ToItem).ToList());
    }

    private static ItemCommand ToItem(ItemDto i) => new(
        i.CodigoProducto, i.Descripcion,
        PorNombre<UnidadMedida>("Catálogo 03 (unidad de medida)", i.UnidadMedida),
        i.Cantidad, i.ValorUnitario,
        PorCodigo<AfectacionIgv>("Catálogo 07 (afectación al IGV)", i.AfectacionIgv),
        string.IsNullOrWhiteSpace(i.SistemaIsc) ? null : PorCodigo<SistemaIsc>("Catálogo 08 (sistema de ISC)", i.SistemaIsc),
        i.TasaIsc, i.MontoFijoIsc, i.AplicaIcbper);

    /// <summary>Catálogos numéricos de SUNAT ("01", "10", "0101"...).</summary>
    private static T PorCodigo<T>(string catalogo, string? valor) where T : struct, Enum
        => int.TryParse((valor ?? "").Trim(), out var n) && Enum.IsDefined(typeof(T), n)
            ? (T)Enum.ToObject(typeof(T), n)
            : throw new DominioValidationException($"'{valor}' no es un código válido del {catalogo}.");

    /// <summary>Catálogos alfabéticos (ISO 4217, UN/ECE Rec. 20).</summary>
    private static T PorNombre<T>(string catalogo, string? valor) where T : struct, Enum
        => Enum.TryParse<T>((valor ?? "").Trim(), ignoreCase: true, out var v) && Enum.IsDefined(v)
            ? v
            : throw new DominioValidationException($"'{valor}' no es un código válido del {catalogo}.");

    public static IReadOnlyList<RegistroMovimientoLegado> ToRecords(MigrateLegacyRequest r)
        => r.Registros.Select(x => new RegistroMovimientoLegado(x.Fecha, x.Ingreso, x.Salida, x.Producto, x.NroDocumento)).ToList();
}
