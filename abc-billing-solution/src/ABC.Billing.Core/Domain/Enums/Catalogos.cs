namespace ABC.Billing.Core.Domain.Enums;

// Catálogos SUNAT del Anexo N.° 8 (RS 097-2012/SUNAT y modificatorias; actualización RS 150-2019/SUNAT).
// OJO: "Anexo 8" es el conjunto de catálogos; cada catálogo tiene su número propio:
//   Catálogo 01 tipo de documento · 02 moneda · 03 unidad de medida · 05 TIPOS DE TRIBUTO (1000, 2000, 7152, 9997...)
//   Catálogo 06 documento de identidad · 07 afectación al IGV · 08 SISTEMA DE CÁLCULO DEL ISC (01, 02, 03)

/// <summary>Catálogo 01 – Código de tipo de documento (cbc:InvoiceTypeCode).</summary>
public enum TipoComprobante
{
    Factura = 1,
    BoletaDeVenta = 3,
    NotaDeCredito = 7,
    NotaDeDebito = 8
}

/// <summary>Catálogo 02 – Tipo de moneda (ISO 4217; cbc:DocumentCurrencyCode).</summary>
public enum TipoMoneda { PEN, USD, EUR }

/// <summary>Catálogo 03 – Unidad de medida comercial (UN/ECE Rec. 20; @unitCode). Subconjunto de la guía SUNAT.</summary>
public enum UnidadMedida
{
    NIU, ZZ, KGM, GRM, LTR, MTR, BX, BG, BO, CT, CA, SET, KT, LBR, KTM, MLL, UM, BJ, CJ, CY
}

/// <summary>Catálogo 06 – Tipo de documento de identidad (cbc:CompanyID/@schemeID). Subconjunto: 0, 1, 4, 6, 7.</summary>
public enum TipoDocumentoIdentidad
{
    NoDomiciliadoSinRuc = 0,
    DNI = 1,
    CarnetExtranjeria = 4,
    RUC = 6,
    Pasaporte = 7
}

/// <summary>Catálogo 07 – Tipo de afectación al IGV (cbc:TaxExemptionReasonCode).</summary>
public enum AfectacionIgv
{
    GravadoOperacionOnerosa = 10,
    GravadoRetiroPorPremio = 11,
    GravadoRetiroPorDonacion = 12,
    GravadoRetiro = 13,
    GravadoRetiroPorPublicidad = 14,
    GravadoBonificaciones = 15,
    GravadoRetiroPorEntregaATrabajadores = 16,
    GravadoIvap = 17,
    ExoneradoOperacionOnerosa = 20,
    ExoneradoTransferenciaGratuita = 21,
    InafectoOperacionOnerosa = 30,
    InafectoRetiroPorBonificacion = 31,
    InafectoRetiro = 32,
    InafectoRetiroPorMuestrasMedicas = 33,
    InafectoRetiroPorConvenioColectivo = 34,
    InafectoRetiroPorPremio = 35,
    InafectoRetiroPorPublicidad = 36,
    Exportacion = 40
}

/// <summary>Clasificación funcional de los códigos del Catálogo 07 (la usa el cálculo y el mapeo a UBL).</summary>
public enum CategoriaAfectacion
{
    GravadoOneroso, GravadoGratuito, Ivap, ExoneradoOneroso, ExoneradoGratuito,
    InafectoOneroso, InafectoGratuito, Exportacion
}

/// <summary>Catálogo 08 – Sistema de cálculo del ISC (cbc:TierRange). Es el ÚNICO uso del Catálogo 08.</summary>
public enum SistemaIsc
{
    AlValor = 1,                 // Apéndice IV, lit. A – T.U.O. IGV e ISC
    MontoFijo = 2,               // Apéndice IV, lit. B
    PrecioVentaPublico = 3       // Apéndice IV, lit. C
}

/// <summary>Catálogo 09 – Tipo de nota de crédito (cbc:ResponseCode).</summary>
public enum TipoNotaCredito
{
    AnulacionDeLaOperacion = 1, AnulacionPorErrorEnElRuc = 2, CorreccionPorErrorEnLaDescripcion = 3,
    DescuentoGlobal = 4, DescuentoPorItem = 5, DevolucionTotal = 6, DevolucionPorItem = 7,
    Bonificacion = 8, DisminucionEnElValor = 9, OtrosConceptos = 10
}

/// <summary>Catálogo 10 – Tipo de nota de débito (cbc:ResponseCode).</summary>
public enum TipoNotaDebito { InteresesPorMora = 1, AumentoEnElValor = 2, PenalidadesOOtrosConceptos = 3 }

/// <summary>Catálogo 16 – Tipo de precio de venta unitario (cbc:PriceTypeCode).</summary>
public enum TipoPrecio { PrecioUnitarioIncluyeIgv = 1, ValorReferencialOperacionNoOnerosa = 2 }

/// <summary>Catálogo 51 – Código de tipo de operación de la factura (cbc:ProfileID).</summary>
public enum TipoOperacion
{
    VentaInterna = 101, Exportacion = 102, NoDomiciliados = 103, VentaInternaAnticipos = 104,
    VentaItinerante = 105, FacturaGuia = 106, VentaArrozPilado = 107, FacturaComprobantePercepcion = 108,
    FacturaGuiaRemitente = 110, FacturaGuiaTransportista = 111
}

public enum EstadoComprobante
{
    Pendiente, Firmado, EnContingencia, Aceptado, AceptadoConObservaciones, Rechazado
}

public static class CatalogoExtensions
{
    public static string Codigo(this TipoComprobante t) => ((int)t).ToString("D2");
    public static string Codigo(this TipoDocumentoIdentidad t) => ((int)t).ToString();
    public static string Codigo(this AfectacionIgv a) => ((int)a).ToString("D2");
    public static string Codigo(this SistemaIsc s) => ((int)s).ToString("D2");
    public static string Codigo(this TipoOperacion o) => ((int)o).ToString("D4");
    public static string Codigo(this TipoPrecio p) => ((int)p).ToString("D2");

    public static CategoriaAfectacion Categoria(this AfectacionIgv a) => a switch
    {
        AfectacionIgv.GravadoOperacionOnerosa => CategoriaAfectacion.GravadoOneroso,
        AfectacionIgv.GravadoRetiroPorPremio or AfectacionIgv.GravadoRetiroPorDonacion or AfectacionIgv.GravadoRetiro
            or AfectacionIgv.GravadoRetiroPorPublicidad or AfectacionIgv.GravadoBonificaciones
            or AfectacionIgv.GravadoRetiroPorEntregaATrabajadores => CategoriaAfectacion.GravadoGratuito,
        AfectacionIgv.GravadoIvap => CategoriaAfectacion.Ivap,
        AfectacionIgv.ExoneradoOperacionOnerosa => CategoriaAfectacion.ExoneradoOneroso,
        AfectacionIgv.ExoneradoTransferenciaGratuita => CategoriaAfectacion.ExoneradoGratuito,
        AfectacionIgv.InafectoOperacionOnerosa => CategoriaAfectacion.InafectoOneroso,
        AfectacionIgv.Exportacion => CategoriaAfectacion.Exportacion,
        _ => CategoriaAfectacion.InafectoGratuito // 31..36
    };

    public static bool EsGratuita(this AfectacionIgv a) => a.Categoria() is CategoriaAfectacion.GravadoGratuito
        or CategoriaAfectacion.ExoneradoGratuito or CategoriaAfectacion.InafectoGratuito;
}
