namespace ABC.Billing.Core.Domain.Services;

public enum ResultadoSunat { Aceptado, ExcepcionSunat, ExcepcionContribuyente, Rechazo, Observacion }

/// <summary>
/// Clasifica los códigos de respuesta según el Manual del Programador (numeral 4.1):
/// 0100–0999 excepciones de SUNAT · 1000–1999 excepciones del contribuyente (formato/estructura) ·
/// 2000–3999 errores que generan rechazo · 4000 en adelante observaciones. 0 = aceptado.
/// </summary>
public static class ClasificadorCodigosSunat
{
    public static ResultadoSunat Clasificar(int codigo) => codigo switch
    {
        0 => ResultadoSunat.Aceptado,
        >= 100 and <= 999 => ResultadoSunat.ExcepcionSunat,
        >= 1000 and <= 1999 => ResultadoSunat.ExcepcionContribuyente,
        >= 2000 and <= 3999 => ResultadoSunat.Rechazo,
        >= 4000 => ResultadoSunat.Observacion,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Código de respuesta fuera de los rangos documentados.")
    };
}
