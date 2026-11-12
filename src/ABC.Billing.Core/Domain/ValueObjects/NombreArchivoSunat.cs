using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.ValueObjects;

/// <summary>
/// Nombres de archivo exigidos por SUNAT (Manual del Programador, numeral 1.2). El XML y el ZIP comparten
/// el nombre y solo cambia la extensión. Ejemplo: 20100066603-01-F001-1.xml / 20100066603-01-F001-1.zip
/// </summary>
public static class NombreArchivoSunat
{
    public static string Comprobante(Ruc ruc, TipoComprobante tipo, SerieComprobante serie)
        => $"{ruc.Value}-{tipo.Codigo()}-{serie.SerieNumero}";

    /// <summary>Comunicación de baja: RRRRRRRRRRR-RA-YYYYMMDD-CCCCC (correlativo de 1 a 5 dígitos).</summary>
    public static string ComunicacionBaja(Ruc ruc, DateOnly fecha, int correlativo)
        => $"{ruc.Value}-RA-{fecha:yyyyMMdd}-{ValidarCorrelativo(correlativo)}";

    /// <summary>Resumen diario de boletas: RRRRRRRRRRR-RC-YYYYMMDD-CCCCC (envíos en bloques de hasta 500 líneas).</summary>
    public static string ResumenDiario(Ruc ruc, DateOnly fecha, int correlativo)
        => $"{ruc.Value}-RC-{fecha:yyyyMMdd}-{ValidarCorrelativo(correlativo)}";

    private static int ValidarCorrelativo(int c)
        => c is >= 1 and <= 99999 ? c : throw new DominioValidationException("El correlativo del archivo debe tener entre 1 y 5 dígitos.");
}
