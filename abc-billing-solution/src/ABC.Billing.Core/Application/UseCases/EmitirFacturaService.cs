using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Core.Application.UseCases;

/// <summary>
/// Orquesta TODO el flujo de emisión. El controlador (capa MVC) solo traduce el DTO al comando:
/// ningún cálculo tributario ocurre fuera de este caso de uso.
/// </summary>
public sealed class EmitirFacturaService(
    Emisor emisor,
    SeriesPorDefecto seriesPorDefecto,
    TaxCalculatorService calculadora,
    ISerieCorrelativoRepository series,
    IUblXmlGenerator generadorXml,
    IUblXsdValidator validadorXsd,
    IDigitalSigner firmador,
    IInvoiceRepository repositorio,
    IReloj reloj) : IEmitirFacturaUseCase
{
    /// <summary>Umbral de S/ 700 que aparece en el Registro de Ventas 14.1 para identificar al adquirente de una boleta.</summary>
    private const decimal UmbralBoletaIdentificada = 700m;

    public async Task<Invoice> EmitirAsync(EmitirFacturaCommand c, CancellationToken ct = default)
    {
        if (c.TipoComprobante is not (TipoComprobante.Factura or TipoComprobante.BoletaDeVenta))
            throw new DominioValidationException("Este caso de uso emite facturas (01) y boletas (03). Las notas tienen su propio caso de uso.");
        if (c.Items.Count == 0)
            throw new DominioValidationException("El comprobante debe tener al menos un ítem.");

        var cliente = new Customer
        {
            TipoDocumento = c.Cliente.TipoDocumento, NumeroDocumento = c.Cliente.NumeroDocumento,
            RazonSocial = c.Cliente.RazonSocial, Direccion = c.Cliente.Direccion
        };
        cliente.Validar();
        if (c.TipoComprobante == TipoComprobante.Factura && c.TipoOperacion != TipoOperacion.Exportacion
            && cliente.TipoDocumento != TipoDocumentoIdentidad.RUC)
            throw new DominioValidationException("La factura exige un adquirente con RUC (Catálogo 06 = 6), salvo exportación.");

        var invoice = new Invoice
        {
            Emisor = emisor,
            TipoComprobante = c.TipoComprobante,
            TipoOperacion = c.TipoOperacion,
            Moneda = c.Moneda,
            Cliente = cliente,
            FechaEmision = reloj.AhoraPeru(),
            Items = c.Items.Select((i, idx) => new InvoiceLine
            {
                NumeroLinea = idx + 1, CodigoProducto = i.CodigoProducto, Descripcion = i.Descripcion,
                UnidadMedida = i.UnidadMedida, Cantidad = i.Cantidad, ValorUnitario = i.ValorUnitario,
                AfectacionIgv = i.AfectacionIgv, SistemaIsc = i.SistemaIsc, TasaIsc = i.TasaIsc,
                MontoFijoIsc = i.MontoFijoIsc, AplicaIcbper = i.AplicaIcbper
            }).ToList()
        };

        calculadora.Calcular(invoice);

        if (c.TipoComprobante == TipoComprobante.BoletaDeVenta
            && invoice.ImporteTotalPagar >= UmbralBoletaIdentificada
            && cliente.TipoDocumento == TipoDocumentoIdentidad.NoDomiciliadoSinRuc)
            throw new DominioValidationException("Boleta de S/ 700 o más: identifique al adquirente (DNI, CE, pasaporte o RUC).");

        // La serie se elige (o se toma la de defecto); el correlativo SIEMPRE lo asigna el servidor, de forma atómica.
        var serie = (c.SerieSolicitada ?? (c.TipoComprobante == TipoComprobante.Factura
            ? seriesPorDefecto.Factura : seriesPorDefecto.Boleta)).Trim().ToUpperInvariant();
        var correlativo = await series.SiguienteAsync(c.TipoComprobante, serie, ct);
        invoice.Series = new DocumentSeries(serie, correlativo);
        if (!invoice.Series.EsCompatibleCon(c.TipoComprobante))
            throw new DominioValidationException($"La serie {serie} no corresponde a una {c.TipoComprobante} (F = factura, B = boleta).");

        var xml = generadorXml.GenerarXmlUbl21(invoice);
        var firmado = firmador.Firmar(xml);              // 1.º firmar (después ya no se puede tocar el XML)
        validadorXsd.Validar(firmado.Xml);               // 2.º validar contra el XSD el documento FINAL, antes de guardarlo/enviarlo
                                                         //     (el XSD exige contenido en ext:ExtensionContent, o sea, la firma)

        invoice.XmlFirmado = firmado.Xml;
        invoice.DigestValue = firmado.DigestValue;
        invoice.Estado = EstadoComprobante.Firmado;
        await repositorio.GuardarAsync(invoice, ct);
        return invoice;
    }
}
