using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ABC.Billing.Presentation.Api.Security;

/// <summary>
/// Autenticación Bearer MÍNIMA con dos tokens de configuración (rol "emisor" y rol "admin"). Sirve para la fase
/// de especificación; en producción se reemplaza por JWT (OAuth2/OIDC) sin tocar los controladores: solo cambia
/// el esquema registrado en Program.cs. Compara en tiempo constante para evitar ataques de temporización.
/// </summary>
public sealed class BearerTokenAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration config) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "Bearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cabecera = Request.Headers.Authorization.ToString();
        if (!cabecera.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = cabecera["Bearer ".Length..].Trim();
        string? rol = Coincide(token, config["Auth:AdminToken"]) ? "admin"
                    : Coincide(token, config["Auth:EmisorToken"]) ? "emisor" : null;
        if (rol is null) return Task.FromResult(AuthenticateResult.Fail("Token inválido."));

        var identidad = new ClaimsIdentity([new Claim(ClaimTypes.Name, rol), new Claim(ClaimTypes.Role, rol)], Scheme.Name);
        // El token "admin" también puede emitir.
        if (rol == "admin") identidad.AddClaim(new Claim(ClaimTypes.Role, "emisor"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Scheme.Name)));
    }

    private static bool Coincide(string recibido, string? esperado)
        => !string.IsNullOrEmpty(esperado)
           && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(recibido)), SHA256.HashData(Encoding.UTF8.GetBytes(esperado)));
}
