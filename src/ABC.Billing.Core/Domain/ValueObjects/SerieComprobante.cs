using System.Text.RegularExpressions;
using ABC.Billing.Core.Domain.Enums;

namespace ABC.Billing.Core.Domain.ValueObjects;

/// <summary>
/// Serie y correlativo del comprobante. Regla SUNAT (Manual del Programador 1.2): la serie son 4 caracteres,
/// el primero la constante F (facturas y sus notas) o B (boletas y sus notas) + 3 alfanuméricos;
/// el correlativo tiene de 1 a 8 dígitos y se inicia en 1. En UBL: serie-correlativo = cbc:ID.
/// </summary>
public sealed record SerieComprobante
{
    // Corrección: la clase [F|B] aceptaba también el carácter '|'. Aquí solo F o B.
    private static readonly Regex Patron = new(@"^[FB][A-Z0-9]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Serie { get; }
    public long Correlativo { get; }

    public SerieComprobante(string serie, long correlativo)
    {
        serie = (serie ?? "").Trim().ToUpperInvariant();
        if (!EsSerieValida(serie))
            throw new DominioValidationException($"Serie compálida '{serie}': debe ser F o B seguida de 3 caracteres alfanuméricos (ej. F001, B001).");
        if (correlativo is < 1 or > 99_999_999)
            throw new DominioValidationException("El correlativo debe estar entre 1 y 99999999.");
        Serie = serie;
        Correlativo = correlativo;
    }

    public static bool EsSerieValida(string serie) => Patron.IsMatch(serie ?? "");

    /// <summary>Valor de cbc:ID y de &lt;serie_numero&gt;, p. ej. F001-1 (sin ceros a la izquierda).</summary>
    public string SerieNumero => $"{Serie}-{Correlativo}";

    /// <summary>Formato de representación impresa, p. ej. F001-00000001.</summary>
    public string SerieNumeroImpreso => $"{Serie}-{Correlativo:D8}";

    public bool EsCompatibleCon(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.Factura => Serie[0] == 'F',
        TipoComprobante.BoletaDeVenta => Serie[0] == 'B',
        _ => true // notas: F si modifican factura, B si modifican boleta (se valida con el documento afectado)
    };

    public override string ToString() => SerieNumero;
}
