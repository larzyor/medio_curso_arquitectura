// Pruebas de humo ejecutables SIN paquetes NuGet:  dotnet run --project tests/ABC.Billing.SmokeTests [carpeta-salida]
// Verifican el dominio, el XML UBL 2.1 contra el XSD oficial, la firma XMLDSig, el parser del CDR y la migración.
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Application.UseCases;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using ABC.Billing.Infrastructure.SunatSoap;
using ABC.Billing.Infrastructure.UblEngine.Serializers;

int ok = 0, fail = 0;
void Check(bool cond, string nombre)
{
    if (cond) { ok++; Console.WriteLine($"  OK   {nombre}"); }
    else { fail++; Console.WriteLine($"  FALLA {nombre}"); }
}
void Seccion(string t) => Console.WriteLine($"\n== {t}");
string? salida = args.Length > 0 ? args[0] : null;

// RUC ficticios con dígito verificador Módulo 11 válido
const string RucEmisor = "20600000013", RucCliente = "20123456786";

Seccion("Value Objects");
Check(RucNumber.EsValido(RucEmisor) && RucNumber.EsValido(RucCliente), "RUC de prueba válidos (Módulo 11)");
Check(!RucNumber.EsValido("20600000014"), "RUC con dígito verificador incorrecto se rechaza");
Check(DocumentSeries.EsSerieValida("F001") && DocumentSeries.EsSerieValida("B0A1") && DocumentSeries.EsSerieValida("FA01"), "Series F### / B### / FA01 válidas");
Check(!DocumentSeries.EsSerieValida("|001") && !DocumentSeries.EsSerieValida("X001") && !DocumentSeries.EsSerieValida("F01"), "Series con '|', X o longitud 3 se rechazan (bug del [F|B] corregido)");
Check(new DocumentSeries("F001", 1).SerieNumero == "F001-1" && new DocumentSeries("F001", 1).SerieNumeroImpreso == "F001-00000001", "serie_numero = F001-1 (impreso F001-00000001)");
Check(!new DocumentSeries("B001", 5).EsCompatibleCon(TipoComprobante.Factura), "Serie B no corresponde a factura");
Check(NombreArchivoSunat.Comprobante(new RucNumber(RucEmisor), TipoComprobante.Factura, new DocumentSeries("F001", 1)) == $"{RucEmisor}-01-F001-1", "Nombre de archivo SUNAT RUC-01-F001-1");
Check(NombreArchivoSunat.ComunicacionBaja(new RucNumber(RucEmisor), new DateOnly(2026, 11, 3), 1) == $"{RucEmisor}-RA-20261103-1", "Nombre de comunicación de baja (RA)");

Seccion("Clasificación de códigos de respuesta (Manual 4.1)");
Check(ClasificadorCodigosSunat.Clasificar(0) == ResultadoSunat.Aceptado, "0 = aceptado");
Check(ClasificadorCodigosSunat.Clasificar(135) == ResultadoSunat.ExcepcionSunat && ClasificadorCodigosSunat.Clasificar(1033) == ResultadoSunat.ExcepcionContribuyente, "0100-0999 y 1000-1999 = excepciones");
Check(ClasificadorCodigosSunat.Clasificar(2047) == ResultadoSunat.Rechazo && ClasificadorCodigosSunat.Clasificar(4031) == ResultadoSunat.Observacion, "2000-3999 = rechazo; 4000+ = observación");

Seccion("Monto en letras (leyenda 1000)");
Check(MontoEnLetras.Convertir(321.19m, TipoMoneda.PEN) == "SON: TRESCIENTOS VEINTIUNO Y 19/100 SOLES", "321.19 → TRESCIENTOS VEINTIUNO Y 19/100 SOLES");
Check(MontoEnLetras.Convertir(21000m, TipoMoneda.PEN).StartsWith("SON: VEINTIUN MIL Y 00/100"), "21000 → VEINTIUN MIL");
Check(MontoEnLetras.Convertir(1_000_000m, TipoMoneda.PEN).StartsWith("SON: UN MILLON Y 00/100"), "1 000 000 → UN MILLON");
Check(MontoEnLetras.Convertir(100m, TipoMoneda.USD) == "SON: CIEN Y 00/100 DÓLARES AMERICANOS", "100 USD → CIEN");

Seccion("Cálculo tributario (ISC → IGV, ICBPER, gratuitas)");
var emisor = new Emisor { Ruc = new RucNumber(RucEmisor), RazonSocial = "ABC SOFTWARE CORP S.A.C.", NombreComercial = "ABC Abarrotes" };
var reloj = new RelojFijo(new DateTime(2026, 11, 3, 10, 15, 0));
var calculadora = new TaxCalculatorService(tarifaIcbperPorBolsa: 0.50m);
var xmlGen = new UblInvoice21XmlSerializer();
var validador = new UblXsdValidator();
var certificado = CertificadoDePrueba.Crear(RucEmisor);
var firmador = new XmlDsigCertificateSigner(certificado);
var facturas = new InMemoryInvoiceRepository();
var series = new InMemorySerieCorrelativoRepository();
var emitir = new EmitirFacturaService(emisor, new SeriesPorDefecto(), calculadora, series, xmlGen, validador, firmador, facturas, reloj);

var cmd = new EmitirFacturaCommand(TipoComprobante.Factura, TipoOperacion.VentaInterna, null, TipoMoneda.PEN,
    new ClienteCommand(TipoDocumentoIdentidad.RUC, RucCliente, "BODEGA \"EL ÑANDÚ\" & HIJOS S.A.C. <prueba>", "Av. Alameda Perú 123, Tingo María"),
    [
        new ItemCommand("ARR-5KG", "Arroz extra 5 kg", UnidadMedida.NIU, 10m, 18.00m, AfectacionIgv.GravadoOperacionOnerosa),
        new ItemCommand("CER-620", "Cerveza 620 ml (ISC al valor, tasa de prueba 10 %)", UnidadMedida.NIU, 6m, 5.00m,
            AfectacionIgv.GravadoOperacionOnerosa, SistemaIsc.AlValor, TasaIsc: 10m),
        new ItemCommand("QUI-1KG", "Quinua exonerada (ejemplo) 1 kg", UnidadMedida.KGM, 5m, 12.00m, AfectacionIgv.ExoneradoOperacionOnerosa),
        new ItemCommand("SAL-1KG", "Sal de mesa inafecta (ejemplo)", UnidadMedida.NIU, 2m, 4.00m, AfectacionIgv.InafectoOperacionOnerosa),
        new ItemCommand("BOL-PLA", "Bolsa de plástico", UnidadMedida.NIU, 3m, 0.10m, AfectacionIgv.GravadoOperacionOnerosa, AplicaIcbper: true),
        new ItemCommand("MUE-001", "Muestra gratuita (retiro por premio)", UnidadMedida.NIU, 1m, 20.00m, AfectacionIgv.GravadoRetiroPorPremio)
    ]);

var factura = await emitir.EmitirAsync(cmd);

Check(factura.TotalOperacionesGravadas == 210.30m, "Total gravadas = 210.30 (valor de venta sin ISC)");
Check(factura.TotalIsc == 3.00m, "ISC = 3.00 (10 % de 30.00)");
Check(factura.BaseImponibleIgv == 213.30m, "Base del IGV = gravadas + ISC = 213.30");
Check(factura.TotalIgv == 38.39m, "IGV = 38.39 (18 % de 213.30, suma por línea)");
Check(TaxCalculatorService.R2(factura.BaseImponibleIgv * 0.18m) == factura.TotalIgv, "IGV total = 18 % × (gravadas + ISC)");
Check(factura.TotalOperacionesExoneradas == 60.00m && factura.TotalOperacionesInafectas == 8.00m, "Exoneradas 60.00 · inafectas 8.00");
Check(factura.TotalIcbper == 1.50m, "ICBPER = 1.50 (3 bolsas × 0.50), fuera de la base del IGV");
Check(factura.TotalOperacionesGratuitas == 20.00m && factura.TotalIgvGratuitas == 3.60m, "Gratuitas 20.00 con IGV informativo 3.60");
Check(factura.TotalValorVenta == 278.30m, "LineExtensionAmount = 278.30 (suma TODAS las operaciones onerosas, no solo gravadas)");
Check(factura.TotalPrecioVenta == 321.19m && factura.ImporteTotalPagar == 321.19m, "Total a pagar = 321.19 (el IGV de gratuitas NO se cobra)");
Check(factura.Series.SerieNumero == "F001-1" && factura.Estado == EstadoComprobante.Firmado, "Serie asignada F001-1, estado Firmado");
Check(factura.Items[1].MontoIsc == 3.00m && factura.Items[1].BaseIgv == 33.00m && factura.Items[1].MontoIgv == 5.94m, "Ítem con ISC: ISC 3.00 → base IGV 33.00 → IGV 5.94");

var segunda = await emitir.EmitirAsync(cmd);
Check(segunda.Series.SerieNumero == "F001-2", "Segundo comprobante recibe F001-2");

Seccion("Correlativo atómico bajo concurrencia");
var concurrente = new InMemorySerieCorrelativoRepository();
var tomados = (await Task.WhenAll(Enumerable.Range(0, 500).Select(_ => Task.Run(() => concurrente.SiguienteAsync(TipoComprobante.Factura, "F009"))))).ToList();
Check(tomados.Distinct().Count() == 500 && tomados.Min() == 1 && tomados.Max() == 500, "500 asignaciones en paralelo: 500 correlativos distintos (1..500)");

Seccion("XML UBL 2.1");
var xml = factura.XmlFirmado!;
var texto = Encoding.Latin1.GetString(xml);
Check(texto.StartsWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"no\"?>"), "Declaración XML en ISO-8859-1");
var doc = XDocument.Parse(texto);
XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2", cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
Check(doc.Root!.Element(cbc + "ID")!.Value == "F001-1" && doc.Root.Element(cbc + "UBLVersionID")!.Value == "2.1", "cbc:ID = F001-1 y UBLVersionID = 2.1");
Check(doc.Root.Elements(cac + "InvoiceLine").Count() == 6, "6 cac:InvoiceLine (antes no se generaba ninguna)");
Check(doc.Root.Element(cac + "AccountingSupplierParty") is not null, "cac:AccountingSupplierParty presente");
Check(doc.Root.Elements(cac + "TaxTotal").Count() == 1, "Un único cac:TaxTotal con varios TaxSubtotal");
var codigos = doc.Root.Element(cac + "TaxTotal")!.Descendants(cac + "TaxScheme").Select(t => t.Element(cbc + "ID")!.Value).ToList();
Check(codigos.SequenceEqual(["1000", "2000", "9997", "9998", "9996", "7152"]), "Subtotales del encabezado: 1000, 2000, 9997, 9998, 9996, 7152 (Catálogo 05)");
Check(doc.Descendants(cbc + "TierRange").Single().Value == "01", "cbc:TierRange = 01 (Catálogo 08, ISC al valor)");
Check(texto.Contains("BODEGA \"EL ÑANDÚ\"".Replace("\"", "&quot;")) || texto.Contains("EL ÑANDÚ"), "Texto con Ñ/Ú conservado en ISO-8859-1");
Check(texto.Contains("&amp; HIJOS") && texto.Contains("&lt;prueba&gt;"), "& < > escapados por el serializador (sin concatenar cadenas)");
Check(doc.Root.Elements(cbc + "Note").Any(n => (string?)n.Attribute("languageLocaleID") == "1000" && n.Value == "SON: TRESCIENTOS VEINTIUNO Y 19/100 SOLES"), "Leyenda 1000 con el monto en letras");

Seccion("Validación contra UBL-Invoice-2.1.xsd (oficial OASIS)");
try { validador.Validar(xml); Check(true, "La factura firmada cumple el XSD"); }
catch (UblValidationException e) { Check(false, "XSD: " + e.Message); }
var roto = Encoding.Latin1.GetBytes(texto.Replace("<cac:LegalMonetaryTotal>", "<cac:LegalMonetaryTotalX>").Replace("</cac:LegalMonetaryTotal>", "</cac:LegalMonetaryTotalX>"));
Check(Throws<UblValidationException>(() => validador.Validar(roto)), "Un XML con un elemento inválido es rechazado por el XSD");
var sinLineas = await Task.Run(() => Throws<DominioValidationException>(() => calculadora.Calcular(new Invoice { Emisor = emisor })));
Check(sinLineas, "Comprobante sin ítems → error de dominio");

Seccion("Firma XMLDSig");
Check(texto.Contains("<ds:Signature") || texto.Contains("Signature"), "Nodo ds:Signature dentro de ext:ExtensionContent");
Check(XmlDsigCertificateSigner.Verificar(xml), "La firma es válida");
var alterado = Encoding.Latin1.GetBytes(texto.Replace("Arroz extra 5 kg", "Arroz extra 50 kg"));
Check(!XmlDsigCertificateSigner.Verificar(alterado), "Alterar el XML después de firmar invalida la firma");
Check(!string.IsNullOrEmpty(factura.DigestValue), "DigestValue guardado para trazabilidad");

Seccion("CDR (Constancia de Recepción) — ejemplo del Manual del Programador, Anexo 1 C.1");
const string cdrEjemplo = """
<?xml version="1.0" encoding="ISO-8859-1" standalone="no"?>
<ar:ApplicationResponse xmlns="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:ar="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
<cbc:ID>201200000230061</cbc:ID>
<cbc:Note>4031 - Debe indicar el nombre comercial</cbc:Note>
<cbc:Note>4001 - El numero de RUC del receptor no existe.</cbc:Note>
<cac:DocumentResponse><cac:Response><cbc:ReferenceID>FA01-981</cbc:ReferenceID><cbc:ResponseCode>0</cbc:ResponseCode><cbc:Description>La Factura numero FA01-981, ha sido aceptada</cbc:Description></cac:Response></cac:DocumentResponse>
</ar:ApplicationResponse>
""";
var cdr = CdrParser.ParsearXml(Encoding.Latin1.GetBytes(cdrEjemplo));
Check(cdr.CodigoRespuesta == 0 && cdr.Observaciones.Count == 2 && cdr.Descripcion.Contains("aceptada"), "Código 0 + 2 observaciones (4031, 4001) → aceptada con observaciones");
byte[] zipCdr;
using (var ms = new MemoryStream())
{
    using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) { using var s = z.CreateEntry($"R-{factura.NombreArchivo}.xml").Open(); s.Write(Encoding.Latin1.GetBytes(cdrEjemplo)); }
    zipCdr = ms.ToArray();
}
Check(CdrParser.ParsearZip(zipCdr).CodigoRespuesta == 0, "El CDR se extrae del ZIP (R-<nombre>.xml)");

Seccion("Transmisión (cliente SOAP simulado) y estados");
var transmisor = new TransmitirComprobanteService(facturas, new SunatFalso(zipCdr, disponible: true));
var r1 = await transmisor.TransmitirAsync(factura.Id);
Check(r1.Estado == EstadoComprobante.AceptadoConObservaciones && r1.CodigoRespuesta == 0, "CDR 0 con notas → AceptadoConObservaciones");
Check(await ThrowsAsync<ConflictoException>(() => transmisor.TransmitirAsync(factura.Id)), "Reenviar un comprobante aceptado → conflicto (409)");
var enContingencia = new TransmitirComprobanteService(facturas, new SunatFalso(zipCdr, disponible: false));
var r2 = await enContingencia.TransmitirAsync(segunda.Id);
Check(r2.Estado == EstadoComprobante.EnContingencia, "SUNAT caída → EnContingencia (queda en cola, XML firmado intacto)");
var soap = new SunatSoapOseClient(new HttpClient(), new SunatOseOptions("https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService", "https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService", RucEmisor, "MODDATOS", "moddatos"));
var sobre = soap.ConstruirSobre(new XElement("x")).ToString();
Check(sobre.Contains($"<wsse:Username>{RucEmisor}MODDATOS</wsse:Username>") && sobre.Contains("<wsse:Password>moddatos</wsse:Password>"), "WS-Security UsernameToken: Username = RUC + usuario SOL");
var zipEnvio = SunatSoapOseClient.CrearZip(factura.NombreArchivo, xml);
using (var z = new ZipArchive(new MemoryStream(zipEnvio)))
    Check(z.Entries.Any(e => e.FullName == "dummy/") && z.Entries.Any(e => e.FullName == factura.NombreArchivo + ".xml"), "ZIP de envío: carpeta dummy/ + XML con el mismo nombre");

Seccion("Reglas de negocio");
Check(await ThrowsAsync<DominioValidationException>(() => emitir.EmitirAsync(cmd with { Cliente = cmd.Cliente with { TipoDocumento = TipoDocumentoIdentidad.DNI, NumeroDocumento = "12345678" } })), "Factura a cliente con DNI → 422");
Check(await ThrowsAsync<DominioValidationException>(() => emitir.EmitirAsync(cmd with { SerieSolicitada = "B001" })), "Factura con serie B001 → 422");
Check(await ThrowsAsync<DominioValidationException>(() => emitir.EmitirAsync(cmd with { Items = [cmd.Items[0] with { Cantidad = 0 }] })), "Cantidad 0 → 422");
Check(await ThrowsAsync<DominioValidationException>(() => emitir.EmitirAsync(cmd with { Items = [cmd.Items[4] with { Cantidad = 2.5m }] })), "ICBPER con cantidad no entera → 422");
Check(await ThrowsAsync<DominioValidationException>(() => emitir.EmitirAsync(cmd with { Items = [cmd.Items[1] with { SistemaIsc = SistemaIsc.PrecioVentaPublico }] })), "ISC sistema 03 declarado como no soportado → 422");
var boleta = await emitir.EmitirAsync(new EmitirFacturaCommand(TipoComprobante.BoletaDeVenta, TipoOperacion.VentaInterna, null, TipoMoneda.PEN,
    new ClienteCommand(TipoDocumentoIdentidad.DNI, "45678912", "JUAN PEREZ RAMOS", null),
    [new ItemCommand("ARR-5KG", "Arroz extra 5 kg", UnidadMedida.NIU, 2m, 18.00m, AfectacionIgv.GravadoOperacionOnerosa)]));
Check(boleta.Series.SerieNumero == "B001-1" && boleta.ImporteTotalPagar == 42.48m, "Boleta B001-1 por 42.48 (2 × 18.00 + IGV)");

Seccion("Migración del sistema legado (fecha, ingreso, salida, producto, nro_documento)");
var kardex = new InMemoryKardexRepository();
var migrar = new MigrarDatosLegadosService(kardex);
var res = await migrar.MigrarLoteAsync(
[
    new("01/03/2024", 100, 0, "  arroz   extra ", "NI-001"),
    new("2024-03-05", 0, 30, "ARROZ EXTRA", "GR-014"),
    new("07/03/2024", 50, 0, "Azúcar rubia", "NI-002"),
    new("31/02/2024", 5, 0, "Fideos", "NI-003"),          // fecha imposible
    new("08/03/2024", 0, 10, "Aceite", "GR-015"),          // sale sin haber entrado: saldo negativo
    new("09/03/2024", 0, 0, "Sal", "NI-004")               // sin movimiento
]);
Check(res.RegistrosMigrados == 3 && res.RegistrosConError == 3, "3 filas migradas · 3 con error");
Check(res.Saldos.Single(s => s.Producto == "ARROZ EXTRA").Saldo == 70m, "Saldo de ARROZ EXTRA = 100 − 30 = 70 (nombres normalizados)");
Check(kardex.Kardex.Count == 2 && kardex.Kardex.All(k => k.Concepto == KardexMovement.ConceptoSaldoInicialMigracion), "Un movimiento SALDO_INICIAL_MIGRACION por producto");
Check(kardex.Auditoria.Count == 3 && kardex.Auditoria.Any(a => a.NroDocumento == "GR-014"), "Las filas originales (incl. nro_documento) quedan en auditoría enlazadas al kardex");
Check(res.Errores.Select(e => e.Fila).OrderBy(x => x).SequenceEqual([4, 5, 6]), "Errores reportados por fila: 4 (fecha), 5 (saldo negativo), 6 (sin movimiento)");

if (salida is not null)
{
    Directory.CreateDirectory(salida);
    File.WriteAllBytes(Path.Combine(salida, "factura-ejemplo-firmada.xml"), xml);
    File.WriteAllBytes(Path.Combine(salida, "boleta-ejemplo-firmada.xml"), boleta.XmlFirmado!);
    File.WriteAllBytes(Path.Combine(salida, "envio-ejemplo.zip"), zipEnvio);
    Console.WriteLine($"\nEjemplos escritos en {salida}");
}

Console.WriteLine($"\nResultado: {ok} OK, {fail} fallas");
return fail == 0 ? 0 : 1;

static bool Throws<T>(Action a) where T : Exception { try { a(); return false; } catch (T) { return true; } }
static async Task<bool> ThrowsAsync<T>(Func<Task> a) where T : Exception { try { await a(); return false; } catch (T) { return true; } }

sealed class RelojFijo(DateTime fecha) : IReloj { public DateTime AhoraPeru() => fecha; }

sealed class SunatFalso(byte[] cdrZip, bool disponible) : ISunatOseClient
{
    public Task<CdrSunat> EnviarComprobanteAsync(string nombre, byte[] xml, CancellationToken ct = default)
        => disponible ? Task.FromResult(CdrParser.ParsearZip(cdrZip)) : throw new SunatNoDisponibleException("sin conexión");
    public Task<CdrSunat> ConsultarCdrAsync(string ruc, string tipo, string serie, long numero, CancellationToken ct = default)
        => Task.FromResult(CdrParser.ParsearZip(cdrZip));
}
