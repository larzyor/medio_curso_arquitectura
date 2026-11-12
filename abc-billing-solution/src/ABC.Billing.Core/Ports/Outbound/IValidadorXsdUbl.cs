namespace ABC.Billing.Core.Ports.Outbound;

public interface IValidadorXsdUbl
{
    /// <summary>Valida el XML (ya firmado) contra UBL-Invoice-2.1.xsd. Lanza <c>UblValidationException</c> si hay errores.</summary>
    void Validar(byte[] xml);
}
