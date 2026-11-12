using System.Text.Json;
using ABC.Billing.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ABC.Billing.Presentation.Api.Middleware;

/// <summary>Traduce las excepciones del dominio/infraestructura a respuestas application/problem+json (RFC 7807).</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception e)
        {
            var (estado, titulo, detalle, extra) = Clasificar(e);
            if (estado >= 500) log.LogError(e, "Error {Estado} en {Ruta}", estado, ctx.Request.Path);
            if (ctx.Response.HasStarted) throw;

            var problema = new ProblemDetails { Status = estado, Title = titulo, Detail = detalle, Instance = ctx.Request.Path };
            if (extra is not null) problema.Extensions["errores"] = extra;
            ctx.Response.Clear();
            ctx.Response.StatusCode = estado;
            await ctx.Response.WriteAsJsonAsync(problema, options: null, contentType: "application/problem+json");
        }
    }

    private static (int, string, string, object?) Clasificar(Exception e) => e switch
    {
        UblValidationException u => (422, "El XML no cumple UBL 2.1", "El comprobante generado no supera la validación contra el XSD.", u.Errores),
        DominioValidationException d => (422, "Regla de negocio incumplida", d.Message, null),
        ConflictoException c => (409, "Conflicto de estado", c.Message, null),
        NoEncontradoException n => (404, "No encontrado", n.Message, null),
        SunatExcepcionException s => (502, "SUNAT respondió con una excepción", s.Message, s.Codigo is null ? null : new { codigo = s.Codigo }),
        SunatNoDisponibleException s => (503, "SUNAT/OSE no disponible", s.Message, null),
        BadHttpRequestException or JsonException => (400, "Solicitud mal formada", "El cuerpo de la solicitud no es un JSON válido.", null),
        _ => (500, "Error interno", "Ocurrió un error inesperado.", null)   // sin detalles internos al cliente
    };
}
