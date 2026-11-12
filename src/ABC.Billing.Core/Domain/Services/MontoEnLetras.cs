using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.Services;

/// <summary>Leyenda 1000 (Catálogo 52): importe total en letras. Ej.: "SON: TRESCIENTOS VEINTIUNO Y 19/100 SOLES".</summary>
public static class MontoEnLetras
{
    private static readonly string[] Menores =
    [
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ", "ONCE", "DOCE", "TRECE",
        "CATORCE", "QUINCE", "DIECISEIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE", "VEINTIUNO", "VEINTIDOS",
        "VEINTITRES", "VEINTICUATRO", "VEINTICINCO", "VEINTISEIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];
    private static readonly string[] Decenas = ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];
    private static readonly string[] Centenas = ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

    public static string Convertir(decimal monto, TipoMoneda moneda)
    {
        monto = Math.Round(Math.Abs(monto), 2, MidpointRounding.AwayFromZero);
        long entero = (long)decimal.Truncate(monto);
        int centimos = (int)((monto - entero) * 100m);
        string nombre = moneda switch { TipoMoneda.PEN => "SOLES", TipoMoneda.USD => "DÓLARES AMERICANOS", _ => "EUROS" };
        return $"SON: {(entero == 0 ? "CERO" : Letras(entero))} Y {centimos:D2}/100 {nombre}";
    }

    private static string Letras(long n)
    {
        if (n >= 1_000_000)
        {
            long mill = n / 1_000_000, resto = n % 1_000_000;
            string cabeza = mill == 1 ? "UN MILLON" : Apocope(Letras(mill)) + " MILLONES";
            return resto == 0 ? cabeza : cabeza + " " + Letras(resto);
        }
        if (n >= 1000)
        {
            long miles = n / 1000, resto = n % 1000;
            string cabeza = miles == 1 ? "MIL" : Apocope(Letras(miles)) + " MIL";
            return resto == 0 ? cabeza : cabeza + " " + Letras(resto);
        }
        return Menor1000((int)n);
    }

    private static string Menor1000(int n)
    {
        if (n == 100) return "CIEN";
        int c = n / 100, r = n % 100;
        string cent = Centenas[c];
        string resto = r < 30 ? Menores[r] : Decenas[r / 10] + (r % 10 > 0 ? " Y " + Menores[r % 10] : "");
        return (cent + " " + resto).Trim();
    }

    // "UNO" delante de MIL/MILLONES pierde la O: VEINTIUN MIL, TREINTA Y UN MIL.
    private static string Apocope(string s) => s.EndsWith("UNO", StringComparison.Ordinal) ? s[..^1] : s;
}
