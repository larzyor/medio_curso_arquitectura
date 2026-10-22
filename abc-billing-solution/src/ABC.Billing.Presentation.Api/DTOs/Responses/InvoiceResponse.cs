namespace ABC.Billing.Presentation.Api.DTOs.Responses;

public class InvoiceResponse
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Identificador SUNAT del comprobante: 'serie_numero'
    /// </summary>
    public string SerieNumero { get; set; } = string.Empty;

    public string TipoComprobante { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public decimal TotalOperacionesGravadas { get; set; }
    public decimal TotalIgv { get; set; }
    public decimal TotalIcbper { get; set; }
    public decimal ImporteTotalPagar { get; set; }
    
    public string Estado { get; set; } = string.Empty;
    public string? DigestValue { get; set; } // Hash de la firma digital
    public string? CdrRespuestaCodigo { get; set; } // "0" = Aceptado por SUNAT
    public string MensajeSunat { get; set; } = string.Empty;
}

public class MigrationReportResponse
{
    public int TotalRegistrosRecibidos { get; set; }
    public int RegistrosMigrados { get; set; }
    public int RegistrosConError { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
