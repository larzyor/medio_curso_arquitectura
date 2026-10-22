using System.Text.RegularExpressions;

namespace ABC.Billing.Core.Domain.ValueObjects;

/// <summary>
/// Encapsula la serie y el correlativo de un comprobante electrónico (ejemplo: F001-00000001).
/// NOTA DE ARQUITECTURA:
/// En el dominio de negocio peruano (SUNAT) esto se denomina 'serie_numero'.
/// En el estándar internacional OASIS UBL 2.1 (UBL-Invoice-2.1.xsd) se mapea a '<cbc:ID>'.
/// </summary>
public sealed record DocumentSeries
{
    public string Serie { get; }
    public int Correlativo { get; }

    /// <summary>
    /// Representación combinada para SUNAT (ejemplo: F001-00000045)
    /// </summary>
    public string SerieNumero => $"{Serie}-{Correlativo:D8}";

    public DocumentSeries(string serie, int correlativo)
    {
        if (string.IsNullOrWhiteSpace(serie) || !Regex.IsMatch(serie, @"^[F|B][A-Z0-9]{3}$"))
            throw new ArgumentException("La serie debe tener 4 caracteres e iniciar con F (Factura) o B (Boleta).", nameof(serie));

        if (correlativo <= 0 || correlativo > 99999999)
            throw new ArgumentOutOfRangeException(nameof(correlativo), "El correlativo debe estar entre 1 y 99999999.");

        Serie = serie.ToUpperInvariant();
        Correlativo = correlativo;
    }

    public static DocumentSeries Parse(string serieNumero)
    {
        var partes = serieNumero.Split('-');
        if (partes.Length != 2 || !int.TryParse(partes[1], out int correlativo))
            throw new FormatException("El formato debe ser 'SERIE-CORRELATIVO' (ejemplo: F001-00000001).");

        return new DocumentSeries(partes[0], correlativo);
    }

    public override string ToString() => SerieNumero;
}
