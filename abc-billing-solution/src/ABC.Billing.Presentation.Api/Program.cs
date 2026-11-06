using System.Security.Cryptography.X509Certificates;
using ABC.Billing.Core.Application.Commands;
using ABC.Billing.Core.Application.UseCases;
using ABC.Billing.Core.Domain.Entities;
using ABC.Billing.Core.Domain.Services;
using ABC.Billing.Core.Domain.ValueObjects;
using ABC.Billing.Core.Ports.Inbound;
using ABC.Billing.Core.Ports.Outbound;
using ABC.Billing.Infrastructure.Cryptography;
using ABC.Billing.Infrastructure.Persistence.InMemory;
using ABC.Billing.Infrastructure.SunatSoap;
using ABC.Billing.Infrastructure.UblEngine.Serializers;
using ABC.Billing.Presentation.Api.Middleware;
using ABC.Billing.Presentation.Api.Security;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Seguridad ----------
if (!builder.Environment.IsDevelopment() && (string.IsNullOrEmpty(cfg["Auth:EmisorToken"]) || string.IsNullOrEmpty(cfg["Auth:AdminToken"])))
    throw new InvalidOperationException("Configure Auth:EmisorToken y Auth:AdminToken (variables de entorno) antes de iniciar fuera de Desarrollo.");
builder.Services.AddAuthentication(BearerTokenAuthHandler.Esquema)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, BearerTokenAuthHandler>(BearerTokenAuthHandler.Esquema, null);
builder.Services.AddAuthorization();
builder.Services.AddControllers();

// ---------- Raíz de composición: aquí (y solo aquí) se conectan puertos con adaptadores ----------
var emisor = new Emisor
{
    Ruc = new RucNumber(cfg["Emisor:Ruc"]!),
    RazonSocial = cfg["Emisor:RazonSocial"]!,
    NombreComercial = cfg["Emisor:NombreComercial"],
    CodigoEstablecimiento = cfg["Emisor:CodigoEstablecimiento"] ?? "0000"
};
X509Certificate2 certificado;
if (!string.IsNullOrWhiteSpace(cfg["Firma:PfxPath"]))
    certificado = new X509Certificate2(cfg["Firma:PfxPath"]!, cfg["Firma:PfxPassword"], X509KeyStorageFlags.Exportable);
else
{
    certificado = CertificadoDePrueba.Crear(emisor.Ruc.Value); // SOLO desarrollo
    Console.WriteLine("ADVERTENCIA: usando un certificado autofirmado de prueba. Configure Firma:PfxPath en producción.");
}
var algoritmo = Enum.TryParse<AlgoritmoFirma>(cfg["Firma:Algoritmo"], true, out var a) ? a : AlgoritmoFirma.RsaSha1;
var sunat = new SunatOseOptions(cfg["Sunat:Endpoint"]!, cfg["Sunat:ConsultaEndpoint"]!, emisor.Ruc.Value, cfg["Sunat:UsuarioSol"]!, cfg["Sunat:ClaveSol"]!);

builder.Services.AddSingleton(emisor);
builder.Services.AddSingleton(new SeriesPorDefecto(cfg["Series:Factura"] ?? "F001", cfg["Series:Boleta"] ?? "B001"));
builder.Services.AddSingleton(new TaxCalculatorService(cfg.GetValue("Tributos:TarifaIcbperPorBolsa", 0.50m)));
builder.Services.AddSingleton<IReloj, RelojPeru>();
builder.Services.AddSingleton<IUblXmlGenerator, UblInvoice21XmlSerializer>();
builder.Services.AddSingleton<IUblXsdValidator>(_ => new UblXsdValidator());
builder.Services.AddSingleton<IDigitalSigner>(_ => new XmlDsigCertificateSigner(certificado, algoritmo));
builder.Services.AddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();           // fase 2: SqlInvoiceRepository
builder.Services.AddSingleton<ISerieCorrelativoRepository, InMemorySerieCorrelativoRepository>();
builder.Services.AddSingleton<IKardexRepository, InMemoryKardexRepository>();
builder.Services.AddHttpClient<ISunatOseClient, SunatSoapOseClient>((http, _) =>
{
    http.Timeout = TimeSpan.FromSeconds(30);
    return new SunatSoapOseClient(http, sunat);
});
builder.Services.AddSingleton<IEmitirFacturaUseCase, EmitirFacturaService>();
builder.Services.AddScoped<ITransmitirComprobanteUseCase, TransmitirComprobanteService>();
builder.Services.AddSingleton<IConsultarComprobanteUseCase, ConsultarComprobanteService>();
builder.Services.AddSingleton<IMigrarDatosLegadosUseCase, MigrarDatosLegadosService>();

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
