using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>Ítem del comprobante (cac:InvoiceLine). Entradas del cliente + valores que calcula el dominio.</summary>
public sealed class LineaComprobante
{
    // ----- Entradas -----
    public int NumeroLinea { get; set; }
    public string CodigoProducto { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public UnidadMedida UnidadMedida { get; set; } = UnidadMedida.NIU;
    public decimal Cantidad { get; set; }

    /// <summary>Valor unitario SIN impuestos. En operaciones gratuitas es el valor referencial unitario.</summary>
    public decimal ValorUnitario { get; set; }
    public AfectacionIgv AfectacionIgv { get; set; } = AfectacionIgv.GravadoOperacionOnerosa;

    /// <summary>Catálogo 08. Null = el ítem no está afecto al ISC.</summary>
    public SistemaIsc? SistemaIsc { get; set; }
    /// <summary>Tasa del ISC en porcentaje (sistema al valor), p. ej. 10 = 10 %.</summary>
    public decimal TasaIsc { get; set; }
    /// <summary>Monto fijo por unidad (sistema de monto fijo).</summary>
    public decimal MontoFijoIsc { get; set; }

    /// <summary>El ítem es una bolsa de plástico afecta al ICBPER (cantidad = número de bolsas).</summary>
    public bool AplicaIcbper { get; set; }

    // ----- Calculados (CalculadoraTributos) -----
    public decimal ValorVenta { get; set; }
    public decimal ValorReferencialTotal { get; set; }
    public decimal MontoIsc { get; set; }
    public decimal BaseIgv { get; set; }
    public decimal MontoIgv { get; set; }
    public decimal MontoIcbper { get; set; }
    public decimal TarifaIcbper { get; set; }
    public decimal PrecioUnitarioConImpuestos { get; set; }

    public void LimpiarCalculo()
    {
        ValorVenta = ValorReferencialTotal = MontoIsc = BaseIgv = MontoIgv = MontoIcbper = TarifaIcbper = PrecioUnitarioConImpuestos = 0m;
    }
}
