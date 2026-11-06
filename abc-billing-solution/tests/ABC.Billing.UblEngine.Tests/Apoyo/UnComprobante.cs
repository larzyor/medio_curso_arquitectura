using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Infrastructure.UblEngine.Serializers;

namespace ABC.Billing.UblEngine.Tests.Apoyo;

/// <summary>
/// Arma comprobantes de prueba en el lenguaje del dominio (cliente, líneas, afectación, ISC, bolsas) y los pasa por
/// el cálculo tributario real. Las pruebas nunca escriben XML a mano: solo describen el comprobante.
/// </summary>
public sealed class UnComprobante
{
    // RUC ficticios con dígito verificador Módulo 11 válido (los mismos de las pruebas de humo).
    public const string RucEmisor = "20600000013";
    public const string RucCliente = "20123456786";
    public static readonly DateTime FechaEmision = new(2026, 11, 3, 10, 15, 0);

    public static readonly Emisor Emisor = new()
    {
        Ruc = new RucNumber(RucEmisor),
        RazonSocial = "ABC SOFTWARE CORP S.A.C.",
        NombreComercial = "ABC Abarrotes"
    };

    private readonly TipoComprobante _tipo;
    private readonly string _serie;
    private TipoMoneda _moneda = TipoMoneda.PEN;
    private Customer _cliente;
    private readonly List<InvoiceLine> _lineas = [];

    private UnComprobante(TipoComprobante tipo, string serie, Customer cliente)
    {
        _tipo = tipo;
        _serie = serie;
        _cliente = cliente;
    }

    public static UnComprobante Factura() => new(TipoComprobante.Factura, "F001",
        new Customer { TipoDocumento = TipoDocumentoIdentidad.RUC, NumeroDocumento = RucCliente, RazonSocial = "BODEGA EL ÑANDÚ S.A.C." });

    public static UnComprobante Boleta() => new(TipoComprobante.BoletaDeVenta, "B001",
        new Customer { TipoDocumento = TipoDocumentoIdentidad.DNI, NumeroDocumento = "45678912", RazonSocial = "JUAN PEREZ RAMOS" });

    public UnComprobante ParaElCliente(string razonSocial)
    {
        _cliente = new Customer { TipoDocumento = _cliente.TipoDocumento, NumeroDocumento = _cliente.NumeroDocumento, RazonSocial = razonSocial };
        return this;
    }

    public UnComprobante EnDolares()
    {
        _moneda = TipoMoneda.USD;
        return this;
    }

    public UnComprobante ConLinea(string descripcion, decimal cantidad, decimal valorUnitario,
        AfectacionIgv afectacion = AfectacionIgv.GravadoOperacionOnerosa, UnidadMedida unidad = UnidadMedida.NIU, string codigo = "")
        => Agregar(new InvoiceLine
        {
            Descripcion = descripcion, Cantidad = cantidad, ValorUnitario = valorUnitario,
            AfectacionIgv = afectacion, UnidadMedida = unidad, CodigoProducto = codigo
        });

    public UnComprobante ConLineaAfectaAlIscAlValor(string descripcion, decimal cantidad, decimal valorUnitario, decimal tasaIsc)
        => Agregar(new InvoiceLine
        {
            Descripcion = descripcion, Cantidad = cantidad, ValorUnitario = valorUnitario,
            AfectacionIgv = AfectacionIgv.GravadoOperacionOnerosa, SistemaIsc = SistemaIsc.AlValor, TasaIsc = tasaIsc
        });

    public UnComprobante ConBolsasPlasticas(int cantidad, decimal valorUnitario = 0.10m)
        => Agregar(new InvoiceLine
        {
            Descripcion = "Bolsa de plástico", Cantidad = cantidad, ValorUnitario = valorUnitario,
            AfectacionIgv = AfectacionIgv.GravadoOperacionOnerosa, AplicaIcbper = true
        });

    private UnComprobante Agregar(InvoiceLine linea)
    {
        linea.NumeroLinea = _lineas.Count + 1;
        _lineas.Add(linea);
        return this;
    }

    /// <summary>El comprobante del dominio con los tributos ya calculados (como lo deja el caso de uso).</summary>
    public Invoice Calculado()
    {
        var comprobante = new Invoice
        {
            Emisor = Emisor,
            TipoComprobante = _tipo,
            TipoOperacion = TipoOperacion.VentaInterna,
            Moneda = _moneda,
            Cliente = _cliente,
            FechaEmision = FechaEmision,
            Series = new DocumentSeries(_serie, 1),
            Items = _lineas
        };
        new TaxCalculatorService(tarifaIcbperPorBolsa: 0.50m).Calcular(comprobante);
        return comprobante;
    }

    /// <summary>Bytes del XML tal como los entrega el adaptador del puerto IUblXmlGenerator.</summary>
    public byte[] XmlGenerado() => new UblInvoice21XmlSerializer().GenerarXmlUbl21(Calculado());

    /// <summary>El XML generado, leído para poder hacer aserciones sobre él.</summary>
    public XmlUbl Xml() => XmlUbl.Leer(XmlGenerado());
}
