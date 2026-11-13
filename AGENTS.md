# AGENTS.md: guía para agentes de IA y para quien modifique este sistema

Proyecto: **ABC Billing**, facturación electrónica UBL 2.1 / SUNAT en C# (.NET 8). Metodología **Spec-First**: primero la
especificación (`docs/ESPECIFICACION_TECNICA_UBL_ABC.md`, `docs/openapi.yaml`), después el código.

## Arquitectura (MVC con visión hexagonal)

| Carpeta | Capa | Qué hace | Qué NO debe hacer |
|:---|:---|:---|:---|
| `src/ABC.Billing.Core` | Dominio y aplicación (el hexágono) | Entidades, catálogos SUNAT, cálculo de tributos, casos de uso y **puertos** (interfaces) | Conocer XML, HTTP, base de datos o SUNAT |
| `src/ABC.Billing.Infrastructure` | Adaptadores de salida | Generación del XML desde las clases del XSD (`UblEngine`), firma XMLDSig, validación XSD, cliente SOAP de SUNAT, repositorios | Calcular impuestos o decidir reglas de negocio |
| `src/ABC.Billing.Presentation.Api` | Adaptador de entrada (MVC) | Controladores delgados, DTOs, autenticación, middleware de errores y la **página web** (`wwwroot/index.html`) | Calcular impuestos, asignar correlativos |
| `tests/ABC.Billing.Tests` | Pruebas xUnit | Dominio, aplicación, generación del XML, infraestructura | |
| `tools/ABC.Billing.GenerarClasesUbl` | Herramienta | Genera las clases C# a partir de `UBL-Invoice-2.1.xsd` | |
| `tools/ABC.Billing.GenerarEjemplos` | Herramienta | Regenera `docs/ejemplos/*.xml` | |
| `tools/verificacion-sunat` | Herramienta | Corre las reglas XSL de SUNAT sobre un XML | |

La raíz de composición es `Program.cs` de la API: es el único lugar donde se conectan puertos con adaptadores.

## Flujo de emisión

`POST /api/v1/invoices` → `InvoicesController` → `InvoiceRequestMapper` (DTO a comando, valida catálogos) →
`EmitirFacturaService`: validar → construir `Comprobante` → `CalculadoraTributos` → validar serie → correlativo →
`GeneradorXmlUblFactura21` (llena las clases del XSD) → `FirmadorXmlDsig` → `ValidadorXsdUbl` → guardar.

## Reglas del proyecto

- El XML se genera con las **clases generadas desde el XSD** y XmlSerializer; nunca con `StringBuilder`.
- Catálogo 05 (tributos: 1000, 2000, 7152, 9997...) no es el Catálogo 08 (solo sistema de ISC). Nombres de tributo del Catálogo 05: `IGV`, `ISC`, `ICBPER`, `EXO`, `INA`, `GRA`, `EXP`, `OTROS`.
- Los importes de una línea gratuita (9996) no cuentan en el `TaxTotal` de la línea (regla 3292).
- Todo cambio en el XML debe pasar `dotnet test` y, si cambia la estructura, validarse en probar-xml.nubefact.com con «Firmar» marcado.
- Los textos de los ejemplos van sin tildes: el XML es ISO-8859-1 y pegarlo en un navegador corrompe las tildes (error 2335).

## Comandos

```bash
dotnet test                                                          # 176 pruebas
dotnet run --project src/ABC.Billing.Presentation.Api                # API + página en http://localhost:5099
dotnet run --project tools/ABC.Billing.GenerarEjemplos -- docs/ejemplos
```

Tokens de desarrollo: `dev-emisor-token`, `dev-admin-token`. Antes de modificar algo, leer la sección correspondiente de la especificación.
