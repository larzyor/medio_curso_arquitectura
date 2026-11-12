// Genera los XML de ejemplo de docs/ejemplos usando el flujo real de emisión (calcula, genera, firma y valida contra el XSD).
// Uso:  dotnet run --project tools/ABC.Billing.GenerarEjemplos -- docs/ejemplos
using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Application.UseCases;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using ABC.Billing.Infrastructure.SunatSoap;
using ABC.Billing.Infrastructure.UblEngine.Generacion;

var salida = args.Length > 0 ? args[0] : "docs/ejemplos";
Directory.CreateDirectory(salida);

const string rucEmisor = "20600000013", rucCliente = "20123456786";   // ficticios, con Módulo 11 válido
var emisor = new Emisor { Ruc = new Ruc(rucEmisor), RazonSocial = "ABC SOFTWARE CORP S.A.C.", NombreComercial = "ABC Abarrotes" };
var servicio = new EmitirFacturaService(emisor, new SeriesPorDefecto(), new CalculadoraTributos(0.50m), new RepositorioCorrelativosEnMemoria(),
    new GeneradorXmlUblFactura21(), new ValidadorXsdUbl(), new FirmadorXmlDsig(CertificadoDePrueba.Crear(rucEmisor)),
    new RepositorioComprobantesEnMemoria(), new Reloj(new DateTime(2026, 11, 3, 10, 15, 0)));

var factura = await servicio.EmitirAsync(new EmitirFacturaCommand(TipoComprobante.Factura, TipoOperacion.VentaInterna, null, TipoMoneda.PEN,
    new ClienteCommand(TipoDocumentoIdentidad.RUC, rucCliente, "BODEGA \"EL ÑANDÚ\" & HIJOS S.A.C. <prueba>", "Av. Alameda Perú 123, Tingo María"),
    [
        new ItemCommand("ARR-5KG", "Arroz extra 5 kg", UnidadMedida.NIU, 10m, 18.00m, AfectacionIgv.GravadoOperacionOnerosa),
        new ItemCommand("CER-620", "Cerveza 620 ml (ISC al valor, tasa de prueba 10 %)", UnidadMedida.NIU, 6m, 5.00m, AfectacionIgv.GravadoOperacionOnerosa, SistemaIsc.AlValor, TasaIsc: 10m),
        new ItemCommand("QUI-1KG", "Quinua exonerada (ejemplo) 1 kg", UnidadMedida.KGM, 5m, 12.00m, AfectacionIgv.ExoneradoOperacionOnerosa),
        new ItemCommand("SAL-1KG", "Sal de mesa inafecta (ejemplo)", UnidadMedida.NIU, 2m, 4.00m, AfectacionIgv.InafectoOperacionOnerosa),
        new ItemCommand("BOL-PLA", "Bolsa de plástico", UnidadMedida.NIU, 3m, 0.10m, AfectacionIgv.GravadoOperacionOnerosa, AplicaIcbper: true),
        new ItemCommand("MUE-001", "Muestra gratuita (retiro por premio)", UnidadMedida.NIU, 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio)
    ]));
var boleta = await servicio.EmitirAsync(new EmitirFacturaCommand(TipoComprobante.BoletaDeVenta, TipoOperacion.VentaInterna, null, TipoMoneda.PEN,
    new ClienteCommand(TipoDocumentoIdentidad.DNI, "45678912", "JUAN PEREZ RAMOS", null),
    [new ItemCommand("ARR-5KG", "Arroz extra 5 kg", UnidadMedida.NIU, 2m, 18.00m, AfectacionIgv.GravadoOperacionOnerosa)]));

File.WriteAllBytes(Path.Combine(salida, "factura-ejemplo-firmada.xml"), factura.XmlFirmado!);
File.WriteAllBytes(Path.Combine(salida, "boleta-ejemplo-firmada.xml"), boleta.XmlFirmado!);
File.WriteAllBytes(Path.Combine(salida, "envio-ejemplo.zip"), ClienteSoapSunat.CrearZip(factura.NombreArchivo, factura.XmlFirmado!));
Console.WriteLine($"{factura.SerieNumero}: total {factura.ImporteTotalPagar}; {boleta.SerieNumero}: total {boleta.ImporteTotalPagar}. Escritos en {salida}");

sealed class Reloj(DateTime fecha) : IReloj { public DateTime AhoraPeru() => fecha; }
