using System.Text.RegularExpressions;

namespace ABC.Billing.Core.Domain.ValueObjects;

/// <summary>
/// Value Object que encapsula un RUC peruano y valida el algoritmo Módulo 11 oficial de SUNAT.
/// </summary>
public sealed record RucNumber
{
    public string Value { get; }

    public RucNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El RUC no puede estar vacío.", nameof(value));

        value = value.Trim();

        if (!Regex.IsMatch(value, @"^(10|15|17|20)\d{9}$"))
            throw new ArgumentException("El RUC debe tener 11 dígitos y comenzar con 10, 15, 17 o 20.", nameof(value));

        if (!ValidarModulo11(value))
            throw new ArgumentException($"El RUC '{value}' no tiene un dígito verificador válido según Módulo 11 de SUNAT.", nameof(value));

        Value = value;
    }

    private static bool ValidarModulo11(string ruc)
    {
        int[] factores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        int suma = 0;

        for (int i = 0; i < 10; i++)
        {
            suma += (ruc[i] - '0') * factores[i];
        }

        int residuo = suma % 11;
        int digitoCalculado = 11 - residuo;

        if (digitoCalculado == 10) digitoCalculado = 0;
        else if (digitoCalculado == 11) digitoCalculado = 1;

        int digitoReal = ruc[10] - '0';
        return digitoCalculado == digitoReal;
    }

    public override string ToString() => Value;
}
