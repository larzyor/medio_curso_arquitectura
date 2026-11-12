using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Services;

/// <summary>
/// Servicio de dominio PURO (sin I/O) que calcula los tributos del comprobante.
/// Orden que fija la Guía de elaboración UBL 2.1 de SUNAT: primero el ISC y después el IGV,
/// porque la base del IGV es (valor de venta + ISC). El ICBPER se cobra aparte y no integra la base del IGV.
/// </summary>
public sealed class CalculadoraTributos
{
    /// <summary>Tasa del IGV (16 % + 2 % de IPM = 18 %).</summary>
    public const decimal TasaIgv = 0.18m;

    /// <summary>Tarifa del ICBPER por bolsa. Se inyecta porque la Ley 30884 la fija por año.</summary>
    public decimal TarifaIcbperPorBolsa { get; }

    public CalculadoraTributos(decimal tarifaIcbperPorBolsa = 0.50m)
    {
        if (tarifaIcbperPorBolsa <= 0) throw new ArgumentOutOfRangeException(nameof(tarifaIcbperPorBolsa));
        TarifaIcbperPorBolsa = tarifaIcbperPorBolsa;
    }

    public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public void Calcular(Comprobante comprobante)
    {
        if (comprobante.Lineas.Count == 0)
            throw new DominioValidationException("El comprobante debe tener al menos un ítem.");

        decimal gravadas = 0, exoneradas = 0, inafectas = 0, exportacion = 0, gratuitas = 0;
        decimal baseIgv = 0, igv = 0, isc = 0, baseIsc = 0, icbper = 0, bolsas = 0, igvGratuitas = 0;

        foreach (var l in comprobante.Lineas)
        {
            l.LimpiarCalculo();
            ValidarLinea(l);

            var categoria = l.AfectacionIgv.Categoria();
            decimal importe = R2(l.Cantidad * l.ValorUnitario);

            switch (categoria)
            {
                case CategoriaAfectacion.GravadoOneroso:
                    l.ValorVenta = importe;
                    l.MontoIsc = CalcularIsc(l, importe);          // 1.º ISC
                    l.BaseIgv = importe + l.MontoIsc;              // base IGV = valor de venta + ISC
                    l.MontoIgv = R2(l.BaseIgv * TasaIgv);          // 2.º IGV
                    gravadas += importe; baseIgv += l.BaseIgv; igv += l.MontoIgv;
                    if (l.MontoIsc > 0) { isc += l.MontoIsc; baseIsc += importe; }
                    break;
                case CategoriaAfectacion.ExoneradoOneroso:
                    l.ValorVenta = importe; exoneradas += importe; break;
                case CategoriaAfectacion.InafectoOneroso:
                    l.ValorVenta = importe; inafectas += importe; break;
                case CategoriaAfectacion.Exportacion:
                    l.ValorVenta = importe; exportacion += importe; break;
                case CategoriaAfectacion.GravadoGratuito:
                    // Retiros: no se cobra; el IGV se informa pero NO forma parte del total a pagar.
                    l.ValorReferencialTotal = importe;
                    l.BaseIgv = importe;
                    l.MontoIgv = R2(importe * TasaIgv);
                    gratuitas += importe; igvGratuitas += l.MontoIgv;
                    break;
                case CategoriaAfectacion.ExoneradoGratuito:
                case CategoriaAfectacion.InafectoGratuito:
                    l.ValorReferencialTotal = importe; gratuitas += importe; break;
                default:
                    throw new DominioValidationException(
                        $"La afectación {l.AfectacionIgv.Codigo()} (IVAP) no está soportada en esta fase.");
            }

            if (l.AplicaIcbper)
            {
                l.TarifaIcbper = TarifaIcbperPorBolsa;
                l.MontoIcbper = R2(l.Cantidad * TarifaIcbperPorBolsa);
                icbper += l.MontoIcbper; bolsas += l.Cantidad;
            }

            // Precio de venta unitario (Catálogo 16): 01 incluye IGV e ISC; 02 = valor referencial (gratuitas).
            l.PrecioUnitarioConImpuestos = categoria is CategoriaAfectacion.GravadoGratuito
                or CategoriaAfectacion.ExoneradoGratuito or CategoriaAfectacion.InafectoGratuito
                ? l.ValorUnitario
                : Math.Round((l.ValorVenta + l.MontoIsc + l.MontoIgv) / l.Cantidad, 10, MidpointRounding.AwayFromZero);
        }

        comprobante.TotalOperacionesGravadas = gravadas;
        comprobante.TotalOperacionesExoneradas = exoneradas;
        comprobante.TotalOperacionesInafectas = inafectas;
        comprobante.TotalExportacion = exportacion;
        comprobante.TotalOperacionesGratuitas = gratuitas;
        comprobante.BaseImponibleIgv = baseIgv;
        comprobante.TotalIgv = igv;
        comprobante.TotalIsc = isc;
        comprobante.TotalIcbper = icbper;
        comprobante.TotalIgvGratuitas = igvGratuitas;

        // Totales (cac:LegalMonetaryTotal): el valor de venta suma TODAS las operaciones onerosas, no solo las gravadas.
        comprobante.TotalValorVenta = gravadas + exoneradas + inafectas + exportacion;
        comprobante.TotalTributos = igv + isc + icbper;
        comprobante.TotalPrecioVenta = comprobante.TotalValorVenta + comprobante.TotalTributos;
        comprobante.ImporteTotalPagar = comprobante.TotalPrecioVenta;

        comprobante.Tributos = ConstruirSubtotales(gravadas, baseIgv, igv, isc, baseIsc, exoneradas, inafectas,
            exportacion, gratuitas, igvGratuitas, icbper, bolsas);
    }

    private decimal CalcularIsc(LineaComprobante l, decimal valorVenta)
    {
        if (l.SistemaIsc is null) return 0m;
        return l.SistemaIsc.Value switch
        {
            SistemaIsc.AlValor => R2(valorVenta * l.TasaIsc / 100m),
            SistemaIsc.MontoFijo => R2(l.Cantidad * l.MontoFijoIsc),
            _ => throw new DominioValidationException(
                "Sistema de ISC 03 (precio de venta al público) no soportado en esta fase: requiere el Apéndice IV, literal C.")
        };
    }

    private static void ValidarLinea(LineaComprobante l)
    {
        if (l.Cantidad <= 0) throw new DominioValidationException($"Ítem {l.NumeroLinea}: la cantidad debe ser mayor que cero.");
        if (l.ValorUnitario <= 0) throw new DominioValidationException($"Ítem {l.NumeroLinea}: el valor unitario debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(l.Descripcion)) throw new DominioValidationException($"Ítem {l.NumeroLinea}: la descripción es obligatoria.");
        if (l.AplicaIcbper && l.Cantidad != decimal.Truncate(l.Cantidad))
            throw new DominioValidationException($"Ítem {l.NumeroLinea}: el ICBPER exige un número entero de bolsas.");
        if (l.SistemaIsc is not null)
        {
            if (l.AfectacionIgv.Categoria() != CategoriaAfectacion.GravadoOneroso)
                throw new DominioValidationException($"Ítem {l.NumeroLinea}: el ISC solo se calcula en operaciones gravadas onerosas (en esta fase).");
            if (l.SistemaIsc == SistemaIsc.AlValor && l.TasaIsc <= 0)
                throw new DominioValidationException($"Ítem {l.NumeroLinea}: el sistema al valor (Catálogo 08 = 01) requiere una tasa de ISC > 0.");
            if (l.SistemaIsc == SistemaIsc.MontoFijo && l.MontoFijoIsc <= 0)
                throw new DominioValidationException($"Ítem {l.NumeroLinea}: el sistema de monto fijo (Catálogo 08 = 02) requiere un monto fijo > 0.");
        }
    }

    private static List<SubtotalTributo> ConstruirSubtotales(decimal gravadas, decimal baseIgv, decimal igv, decimal isc,
        decimal baseIsc, decimal exoneradas, decimal inafectas, decimal exportacion, decimal gratuitas,
        decimal igvGratuitas, decimal icbper, decimal bolsas)
    {
        var lista = new List<SubtotalTributo>();
        if (gravadas > 0) lista.Add(new SubtotalTributo { Tributo = TributoSunat.Igv, BaseImponible = baseIgv, Monto = igv });
        if (isc > 0) lista.Add(new SubtotalTributo { Tributo = TributoSunat.Isc, BaseImponible = baseIsc, Monto = isc });
        if (exoneradas > 0) lista.Add(new SubtotalTributo { Tributo = TributoSunat.Exonerado, BaseImponible = exoneradas, Monto = 0m });
        if (inafectas > 0) lista.Add(new SubtotalTributo { Tributo = TributoSunat.Inafecto, BaseImponible = inafectas, Monto = 0m });
        if (exportacion > 0) lista.Add(new SubtotalTributo { Tributo = TributoSunat.Exportacion, BaseImponible = exportacion, Monto = 0m });
        if (gratuitas > 0) lista.Add(new SubtotalTributo { Tributo = TributoSunat.Gratuito, BaseImponible = gratuitas, Monto = igvGratuitas });
        if (icbper > 0)
            lista.Add(new SubtotalTributo { Tributo = TributoSunat.Icbper, Monto = icbper, CantidadBolsas = bolsas, TarifaPorBolsa = icbper / bolsas });
        return lista;
    }
}
