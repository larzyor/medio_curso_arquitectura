using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Services;

/// <summary>
/// Servicio de Dominio para el cálculo tributario oficial de SUNAT y Catálogo 08.
/// </summary>
public class TaxCalculatorService
{
    private const decimal TASA_IGV = 0.18m;
    private const decimal TARIFA_ICBPER_POR_BOLSA = 0.50m; // S/ 0.50 según ley anual de bolsas plásticas

    public void CalcularTotalesEImpuestos(Invoice invoice)
    {
        decimal gravadas = 0;
        decimal exoneradas = 0;
        decimal inafectas = 0;
        decimal totalIgv = 0;
        decimal totalIcbper = 0;

        foreach (var item in invoice.Items)
        {
            decimal subtotal = item.Cantidad * item.ValorUnitario;

            switch (item.AfectacionIgv)
            {
                case AfectacionIgv.GravadoOperacionOnerosa:
                    gravadas += subtotal;
                    item.MontoIgv = Math.Round(subtotal * TASA_IGV, 2, MidpointRounding.AwayFromZero);
                    totalIgv += item.MontoIgv;
                    break;

                case AfectacionIgv.ExoneradoOperacionOnerosa:
                    exoneradas += subtotal;
                    item.MontoIgv = 0;
                    break;

                case AfectacionIgv.InafectoOperacionOnerosa:
                    inafectas += subtotal;
                    item.MontoIgv = 0;
                    break;
            }

            if (item.AplicaIcbper)
            {
                item.MontoIcbper = Math.Round(item.Cantidad * TARIFA_ICBPER_POR_BOLSA, 2, MidpointRounding.AwayFromZero);
                totalIcbper += item.MontoIcbper;
            }
            else
            {
                item.MontoIcbper = 0;
            }

            item.PrecioUnitario = item.ValorUnitario + (item.MontoIgv / (item.Cantidad == 0 ? 1 : item.Cantidad));
        }

        invoice.TotalOperacionesGravadas = gravadas;
        invoice.TotalOperacionesExoneradas = exoneradas;
        invoice.TotalOperacionesInafectas = inafectas;
        invoice.TotalIgv = totalIgv;
        invoice.TotalIcbper = totalIcbper;
        invoice.ImporteTotalPagar = gravadas + exoneradas + inafectas + totalIgv + totalIcbper;

        // Generar subconjunto de Catálogo 08
        invoice.Tributos.Clear();
        if (totalIgv > 0)
        {
            invoice.Tributos.Add(new TaxSubtotal
            {
                CodigoTributo = CodigoTributo.IGV,
                NombreTributo = "IGV",
                CodigoInternacional = "VAT",
                BaseImponible = gravadas,
                Porcentaje = 18.00m,
                MontoTributo = totalIgv
            });
        }

        if (totalIcbper > 0)
        {
            invoice.Tributos.Add(new TaxSubtotal
            {
                CodigoTributo = CodigoTributo.ICBPER,
                NombreTributo = "ICBPER",
                CodigoInternacional = "OTH",
                BaseImponible = totalIcbper / TARIFA_ICBPER_POR_BOLSA,
                Porcentaje = TARIFA_ICBPER_POR_BOLSA,
                MontoTributo = totalIcbper
            });
        }
    }
}
