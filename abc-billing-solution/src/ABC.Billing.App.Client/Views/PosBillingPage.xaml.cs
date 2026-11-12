namespace ABC.Billing.App.Client.Views;

/// <summary>
/// Vista de Punto de Venta (Mostrador) para terminales Windows de escritorio o Android.
/// </summary>
public class PosBillingPage
{
    public string Titulo => "Caja Rápida - Facturación Electrónica ABC Software Corp";

    public void OnEmitirFacturaClicked()
    {
        // Orquesta la llamada al Controlador MVC y refresca la vista
    }
}
