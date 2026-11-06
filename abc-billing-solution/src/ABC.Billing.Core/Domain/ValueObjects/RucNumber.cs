using System.Text.RegularExpressions;

namespace ABC.Billing.Core.Domain.ValueObjects;

/// <summary>Value Object de un RUC peruano con validación Módulo 11 (dígito verificador SUNAT).</summary>
public sealed record RucNumber
{
    public string Value { get; }

    public RucNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DominioValidationException("El RUC no puede estar vacío.");

        value = value.Trim();

        if (!Regex.IsMatch(value, @"^(10|15|17|20)\d{9}$"))
            throw new DominioValidationException("El RUC debe tener 11 dígitos y comenzar con 10, 15, 17 o 20.");

        if (!ValidarModulo11(value))
            throw new DominioValidationException($"El RUC '{value}' no tiene un dígito verificador válido (Módulo 11).");

        Value = value;
    }

    public static bool EsValido(string? value)
    {
        try { _ = new RucNumber(value ?? ""); return true; }
        catch (DominioValidationException) { return false; }
    }

    private static bool ValidarModulo11(string ruc)
    {
        int[] factores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        int suma = 0;
        for (int i = 0; i < 10; i++) suma += (ruc[i] - '0') * factores[i];

        int digito = 11 - (suma % 11);
        if (digito == 10) digito = 0;
        else if (digito == 11) digito = 1;

        return digito == ruc[10] - '0';
    }

    public override string ToString() => Value;
}
