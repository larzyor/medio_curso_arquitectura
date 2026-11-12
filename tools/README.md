# Herramientas

| Herramienta | Para qué sirve | Comando |
|:---|:---|:---|
| `ABC.Billing.GenerarClasesUbl` | Genera las clases C# de UBL 2.1 **a partir de `UBL-Invoice-2.1.xsd`** (carpeta `UblEngine/Generado`) | `dotnet run --project tools/ABC.Billing.GenerarClasesUbl -- src/ABC.Billing.Infrastructure/UblEngine/Schemas/maindoc/UBL-Invoice-2.1.xsd src/ABC.Billing.Infrastructure/UblEngine/Generado/UblInvoice21.generated.cs` |
| `ABC.Billing.GenerarEjemplos` | Emite una factura y una boleta de ejemplo con el flujo real (calcula, genera, firma y valida) y las guarda en `docs/ejemplos` | `dotnet run --project tools/ABC.Billing.GenerarEjemplos -- docs/ejemplos` |

El archivo generado no se edita a mano: si cambia el XSD, se vuelve a generar.
