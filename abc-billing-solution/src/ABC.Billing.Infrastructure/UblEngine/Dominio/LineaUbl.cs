using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Infrastructure.UblEngine.Generated;
using static ABC.Billing.Infrastructure.UblEngine.Dominio.VocabularioUbl;

namespace ABC.Billing.Infrastructure.UblEngine.Dominio;

/// <summary>
/// Una línea (ítem) del comprobante expresada en UBL: cantidad, valor de venta, precio, producto y los tributos de la
/// línea en el orden de cálculo del dominio (ISC → IGV según la afectación → ICBPER).
/// </summary>
public sealed class LineaUbl
{
    private readonly InvoiceLine _linea;
    private readonly TipoMoneda _moneda;
    private readonly TaxTotalType _tributos = new();
    private decimal _totalTributos;

    /// <summary>18 %, expresado como lo pide cbc:Percent.</summary>
    private const decimal TasaIgvPorcentual = TaxCalculatorService.TasaIgv * 100m;

    private LineaUbl(InvoiceLine linea, TipoMoneda moneda)
    {
        _linea = linea;
        _moneda = moneda;
    }

    /// <summary>Traduce un ítem ya calculado por <c>TaxCalculatorService</c>.</summary>
    public static InvoiceLineType Desde(InvoiceLine linea, TipoMoneda moneda)
        => new LineaUbl(linea, moneda)
            .ConIscSiCorresponde()
            .ConIgvSegunSuAfectacion()
            .ConIcbperSiEsBolsaPlastica()
            .Construir();

    private bool EsGratuita => _linea.AfectacionIgv.EsGratuita();

    private LineaUbl ConIscSiCorresponde()
    {
        if (_linea.MontoIsc <= 0 || _linea.SistemaIsc is null) return this;
        var sistema = _linea.SistemaIsc.Value;
        decimal? tasa = sistema == SistemaIsc.AlValor ? _linea.TasaIsc : null;
        return AgregarTributo(TributoSunat.Isc, _linea.ValorVenta, _linea.MontoIsc, porcentaje: tasa, sistemaIsc: sistema);
    }

    private LineaUbl ConIgvSegunSuAfectacion()
    {
        var afectacion = _linea.AfectacionIgv;
        return afectacion.Categoria() switch
        {
            CategoriaAfectacion.GravadoOneroso =>
                AgregarTributo(TributoSunat.Igv, _linea.BaseIgv, _linea.MontoIgv, porcentaje: TasaIgvPorcentual, afectacion: afectacion),
            CategoriaAfectacion.GravadoGratuito =>   // IGV informativo sobre el valor referencial; no se cobra
                AgregarTributo(TributoSunat.Gratuito, _linea.ValorReferencialTotal, _linea.MontoIgv, porcentaje: TasaIgvPorcentual, afectacion: afectacion),
            CategoriaAfectacion.ExoneradoOneroso =>
                AgregarTributo(TributoSunat.Exonerado, _linea.ValorVenta, 0m, afectacion: afectacion),
            CategoriaAfectacion.InafectoOneroso =>
                AgregarTributo(TributoSunat.Inafecto, _linea.ValorVenta, 0m, afectacion: afectacion),
            CategoriaAfectacion.Exportacion =>
                AgregarTributo(TributoSunat.Exportacion, _linea.ValorVenta, 0m, afectacion: afectacion),
            // exonerado / inafecto gratuitos
            _ => AgregarTributo(TributoSunat.Gratuito, _linea.ValorReferencialTotal, 0m, afectacion: afectacion)
        };
    }

    private LineaUbl ConIcbperSiEsBolsaPlastica()
    {
        if (!_linea.AplicaIcbper) return this;
        _tributos.TaxSubtotal.Add(new TaxSubtotalType
        {
            TaxAmount = Importe(_linea.MontoIcbper, _moneda),
            BaseUnitMeasure = new MeasureType { Value = _linea.Cantidad.ToString("0", System.Globalization.CultureInfo.InvariantCulture), UnitCode = "NIU" },
            TaxCategory = CategoriaTributaria(TributoSunat.Icbper, montoPorUnidad: Importe(_linea.TarifaIcbper, _moneda))
        });
        _totalTributos += _linea.MontoIcbper;
        return this;
    }

    private LineaUbl AgregarTributo(TributoSunat tributo, decimal baseImponible, decimal monto,
        decimal? porcentaje = null, AfectacionIgv? afectacion = null, SistemaIsc? sistemaIsc = null)
    {
        _tributos.TaxSubtotal.Add(new TaxSubtotalType
        {
            TaxableAmount = Importe(baseImponible, _moneda),
            TaxAmount = Importe(monto, _moneda),
            TaxCategory = CategoriaTributaria(tributo, porcentaje, afectacion, sistemaIsc)
        });
        _totalTributos += monto;
        return this;
    }

    private InvoiceLineType Construir()
    {
        _tributos.TaxAmount = Importe(_totalTributos, _moneda);

        var precioDeReferencia = new PriceType
        {
            // Gratuita: valor referencial (Catálogo 16 = 02). Onerosa: precio unitario con impuestos (01).
            PriceAmount = PrecioUnitario(EsGratuita ? _linea.ValorUnitario : _linea.PrecioUnitarioConImpuestos, _moneda),
            PriceTypeCode = CodigoDeCatalogo(
                (EsGratuita ? TipoPrecio.ValorReferencialOperacionNoOnerosa : TipoPrecio.PrecioUnitarioIncluyeIgv).Codigo(),
                "Tipo de Precio", "16")
        };

        var producto = new ItemType();
        producto.Description.Add(Texto(_linea.Descripcion));
        if (!string.IsNullOrWhiteSpace(_linea.CodigoProducto))
            producto.SellersItemIdentification = new ItemIdentificationType { ID = Identificador(_linea.CodigoProducto) };

        var referenciaDePrecio = new PricingReferenceType();
        referenciaDePrecio.AlternativeConditionPrice.Add(precioDeReferencia);

        var linea = new InvoiceLineType
        {
            ID = Identificador(_linea.NumeroLinea.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            InvoicedQuantity = Cantidad(_linea.Cantidad, _linea.UnidadMedida),
            LineExtensionAmount = Importe(_linea.ValorVenta, _moneda),
            PricingReference = referenciaDePrecio,
            Item = producto,
            // Valor unitario sin impuestos; en una entrega gratuita no hay valor cobrado.
            Price = new PriceType { PriceAmount = PrecioUnitario(EsGratuita ? 0m : _linea.ValorUnitario, _moneda) }
        };
        linea.TaxTotal.Add(_tributos);
        return linea;
    }
}
