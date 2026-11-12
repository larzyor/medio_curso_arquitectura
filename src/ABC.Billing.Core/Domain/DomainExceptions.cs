namespace ABC.Billing.Core.Domain;

/// <summary>Regla de negocio o de catálogo incumplida. La API la traduce a HTTP 422.</summary>
public class DominioValidationException(string message) : Exception(message);

/// <summary>Estado incompatible con la operación pedida (p. ej. reenviar un comprobante aceptado). HTTP 409.</summary>
public class ConflictoException(string message) : Exception(message);

/// <summary>Recurso inexistente. HTTP 404.</summary>
public class NoEncontradoException(string message) : Exception(message);

/// <summary>SUNAT/OSE no responde (timeout, red, 5xx sin SOAP Fault). Activa contingencia o HTTP 503.</summary>
public class SunatNoDisponibleException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>SUNAT respondió con un SOAP Fault (excepción: el documento queda como NO informado). HTTP 502.</summary>
public class SunatExcepcionException(int? codigo, string message) : Exception(message)
{
    public int? Codigo { get; } = codigo;
}

/// <summary>El XML generado no cumple el XSD UBL 2.1. Se detecta ANTES de firmar y enviar. HTTP 422.</summary>
public class UblValidationException(IReadOnlyList<string> errores)
    : Exception("El XML no cumple UBL-Invoice-2.1.xsd: " + string.Join(" | ", errores.Take(5)))
{
    public IReadOnlyList<string> Errores { get; } = errores;
}
