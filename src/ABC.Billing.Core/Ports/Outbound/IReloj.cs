namespace ABC.Billing.Core.Ports.Outbound;

/// <summary>Fuente de la hora. Perú es UTC-5 todo el año. Permite pruebas deterministas.</summary>
public interface IReloj
{
    DateTime AhoraPeru();
}
