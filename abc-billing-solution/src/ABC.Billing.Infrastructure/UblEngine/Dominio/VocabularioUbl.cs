using System.Globalization;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Infrastructure.UblEngine.Generated;
using ABC.Billing.Infrastructure.UblEngine.Model;

namespace ABC.Billing.Infrastructure.UblEngine.Dominio;

/// <summary>
/// Cómo se escribe en UBL cada valor del dominio: un importe en una moneda, una cantidad en una unidad de medida,
/// un código de un catálogo SUNAT, un documento de identidad. Es el ÚNICO lugar que conoce los atributos
/// (currencyID, listURI, schemeID...) y el formato numérico; el resto de la capa solo pide "un importe" o "un código".
/// </summary>
internal static class VocabularioUbl
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    internal const string AgenciaUnece = "United Nations Economic Commission for Europe";

    public static IdentifierType Identificador(string valor) => new() { Value = valor };
    public static TextType Texto(string valor) => new() { Value = valor };
    public static NameType Nombre(string valor) => new() { Value = valor };

    /// <summary>Importe con 2 decimales en la moneda del comprobante (cbc:*Amount/@currencyID).</summary>
    public static AmountType Importe(decimal monto, TipoMoneda moneda)
        => new() { CurrencyID = moneda.ToString(), Value = monto.ToString("F2", Inv) };

    /// <summary>Precio o valor unitario: hasta 10 decimales, como permite la guía SUNAT.</summary>
    public static AmountType PrecioUnitario(decimal monto, TipoMoneda moneda)
        => new() { CurrencyID = moneda.ToString(), Value = monto.ToString("0.00########", Inv) };

    /// <summary>Cantidad en la unidad de medida del Catálogo 03 (UN/ECE Rec. 20).</summary>
    public static QuantityType Cantidad(decimal cantidad, UnidadMedida unidad) => new()
    {
        Value = cantidad.ToString("0.00##", Inv),
        UnitCode = unidad.ToString(),
        UnitCodeListID = "UN/ECE rec 20",
        UnitCodeListAgencyName = AgenciaUnece
    };

    public static PercentType Porcentaje(decimal porcentaje) => new() { Value = porcentaje.ToString("0.00", Inv) };

    public static DateType Fecha(DateTime fecha) => new() { Value = fecha.ToString("yyyy-MM-dd", Inv) };
    public static TimeType Hora(DateTime fecha) => new() { Value = fecha.ToString("HH:mm:ss", Inv) };

    /// <summary>Código de un catálogo SUNAT del Anexo 8 (atributos list*).</summary>
    public static CodeType CodigoDeCatalogo(string codigo, string nombreCatalogo, string numeroCatalogo) => new()
    {
        Value = codigo,
        ListAgencyName = UblNs.Agency,
        ListName = nombreCatalogo,
        ListURI = UblNs.Catalogo(numeroCatalogo)
    };

    /// <summary>Identificador validado contra un catálogo SUNAT (atributos scheme*), p. ej. el tipo de operación.</summary>
    public static IdentifierType IdentificadorDeCatalogo(string codigo, string nombreCatalogo, string numeroCatalogo) => new()
    {
        Value = codigo,
        SchemeName = nombreCatalogo,
        SchemeAgencyName = UblNs.Agency,
        SchemeURI = UblNs.Catalogo(numeroCatalogo)
    };

    /// <summary>Número de documento con su tipo del Catálogo 06 (RUC = 6, DNI = 1...).</summary>
    public static IdentifierType DocumentoDeIdentidad(TipoDocumentoIdentidad tipo, string numero) => new()
    {
        Value = numero,
        SchemeID = tipo.Codigo(),
        SchemeName = "Documento de Identidad",
        SchemeAgencyName = UblNs.Agency,
        SchemeURI = UblNs.Catalogo("06")
    };

    /// <summary>Moneda ISO 4217 (Catálogo 02).</summary>
    public static CodeType Moneda(TipoMoneda moneda) => new()
    {
        Value = moneda.ToString(),
        ListID = "ISO 4217 Alpha",
        ListName = "Currency",
        ListAgencyName = AgenciaUnece
    };

    /// <summary>
    /// Tributo del Catálogo 05 en la forma que pide UBL: categoría UN/ECE 5305 + esquema tributario (código, nombre y
    /// código internacional). La afectación al IGV, el porcentaje y el sistema de ISC son opcionales.
    /// </summary>
    public static TaxCategoryType CategoriaTributaria(TributoSunat tributo, decimal? porcentaje = null,
        AfectacionIgv? afectacion = null, SistemaIsc? sistemaIsc = null, AmountType? montoPorUnidad = null) => new()
    {
        ID = new IdentifierType
        {
            Value = tributo.CategoriaUbl,
            SchemeID = "UN/ECE 5305",
            SchemeName = "Tax Category Identifier",
            SchemeAgencyName = AgenciaUnece
        },
        Percent = porcentaje is null ? null : Porcentaje(porcentaje.Value),
        PerUnitAmount = montoPorUnidad,
        TaxExemptionReasonCode = afectacion is null ? null
            : CodigoDeCatalogo(afectacion.Value.Codigo(), "Afectacion del IGV", "07"),
        // cbc:TierRange es texto en UBL 2.1: no admite atributos de catálogo (el XSD los rechaza).
        TierRange = sistemaIsc is null ? null : Texto(sistemaIsc.Value.Codigo()),
        TaxScheme = new TaxSchemeType
        {
            ID = IdentificadorDeCatalogo(tributo.Codigo, "Codigo de tributos", "05"),
            Name = Nombre(tributo.Nombre),
            TaxTypeCode = new CodeType { Value = tributo.CodigoInternacional }
        }
    };
}
