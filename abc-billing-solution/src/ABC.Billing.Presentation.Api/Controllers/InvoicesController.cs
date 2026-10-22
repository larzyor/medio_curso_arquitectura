using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Presentation.Api.DTOs.Requests;
using ABC.Billing.Presentation.Api.DTOs.Responses;

namespace ABC.Billing.Presentation.Api.Controllers;

/// <summary>
/// Controlador MVC para la gestión y emisión de facturación electrónica UBL 2.1.
/// </summary>
public class InvoicesController
{
    private readonly IEmitirFacturaUseCase _emitirFacturaUseCase;
    private readonly TaxCalculatorService _taxCalculatorService;

    public InvoicesController(IEmitirFacturaUseCase emitirFacturaUseCase, TaxCalculatorService taxCalculatorService)
    {
        _emitirFacturaUseCase = emitirFacturaUseCase;
        _taxCalculatorService = taxCalculatorService;
    }

    /// <summary>
    /// POST /api/v1/invoices
    /// Recibe el request, valida reglas SUNAT y emite el comprobante electrónico.
    /// </summary>
    public async Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        // 1. Validar identificación y Módulo 11 si es RUC
        if (request.Cliente.TipoDocumento == "6")
        {
            _ = new RucNumber(request.Cliente.NumeroDocumento); // Lanza ArgumentException si el dígito verificador es inválido
        }

        // 2. Mapear DTO a Entidad de Dominio
        var invoice = new Invoice
        {
            TipoComprobante = (TipoComprobante)int.Parse(request.TipoComprobante),
            Series = string.IsNullOrEmpty(request.SerieNumero) 
                ? new DocumentSeries("F001", 1) 
                : DocumentSeries.Parse(request.SerieNumero),
            Cliente = new Customer(
                (TipoDocumentoIdentidad)int.Parse(request.Cliente.TipoDocumento),
                request.Cliente.NumeroDocumento,
                request.Cliente.RazonSocial,
                request.Cliente.Direccion
            ),
            Items = request.Items.Select((item, index) => new InvoiceLine
            {
                LineNumber = index + 1,
                CodigoProducto = item.Codigo,
                Descripcion = item.Descripcion,
                UnidadMedida = item.UnidadMedida,
                Cantidad = item.Cantidad,
                ValorUnitario = item.ValorUnitario,
                AfectacionIgv = (AfectacionIgv)int.Parse(item.TipoAfectacionIgv),
                AplicaIcbper = item.AplicaIcbper
            }).ToList()
        };

        // 3. Aplicar reglas de cálculo tributario (Catálogo 08)
        _taxCalculatorService.CalcularTotalesEImpuestos(invoice);

        // 4. Ejecutar Caso de Uso (Generación UBL, Firma y envío a SUNAT)
        var result = await _emitirFacturaUseCase.EmitirYTransmitirAsync(invoice, ct);

        // 5. Retornar Response
        return new InvoiceResponse
        {
            Id = result.Id,
            SerieNumero = result.SerieNumero,
            TipoComprobante = request.TipoComprobante,
            FechaEmision = result.FechaEmision,
            TotalOperacionesGravadas = result.TotalOperacionesGravadas,
            TotalIgv = result.TotalIgv,
            TotalIcbper = result.TotalIcbper,
            ImporteTotalPagar = result.ImporteTotalPagar,
            Estado = result.Estado,
            DigestValue = result.DigestValue,
            CdrRespuestaCodigo = result.CdrRespuestaCodigo,
            MensajeSunat = result.CdrRespuestaCodigo == "0" ? "Comprobante aceptado por SUNAT." : "Comprobante observado o pendiente."
        };
    }
}
