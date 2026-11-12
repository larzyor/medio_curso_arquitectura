using System.Text.RegularExpressions;
using ABC.Billing.Core.Domain.Enums;
using ABC.Billing.Core.Domain.ValueObjects;

namespace ABC.Billing.Core.Domain.Entities;

/// <summary>Adquirente o usuario. cac:AccountingCustomerParty (tipo y número según Catálogo 06).</summary>
public sealed class Cliente
{
    public TipoDocumentoIdentidad TipoDocumento { get; set; } = TipoDocumentoIdentidad.RUC;
    public string NumeroDocumento { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string? Direccion { get; set; }

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(RazonSocial))
            throw new DominioValidationException("La razón social o nombre del cliente es obligatoria.");

        var nro = (NumeroDocumento ?? "").Trim();
        switch (TipoDocumento)
        {
            case TipoDocumentoIdentidad.RUC:
                _ = new Ruc(nro); // valida longitud, prefijo y Módulo 11
                break;
            case TipoDocumentoIdentidad.DNI:
                if (!Regex.IsMatch(nro, @"^\d{8}$")) throw new DominioValidationException("El DNI debe tener 8 dígitos.");
                break;
            case TipoDocumentoIdentidad.CarnetExtranjeria:
            case TipoDocumentoIdentidad.Pasaporte:
                if (!Regex.IsMatch(nro, @"^[A-Za-z0-9]{1,12}$")) throw new DominioValidationException("Documento de identidad compálido (máx. 12 alfanuméricos).");
                break;
            default:
                if (nro.Length is 0 or > 15) throw new DominioValidationException("Documento de identidad compálido (use '-' si no existe).");
                break;
        }
        NumeroDocumento = nro;
    }
}
