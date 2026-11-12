# ABC Billing: facturación electrónica UBL 2.1 / SUNAT

Código de referencia de la especificación Spec-First (Taller 2: **generar el XML de la factura desde el XSD**).
La especificación está en [`docs/ESPECIFICACION_TECNICA_UBL_ABC.md`](docs/ESPECIFICACION_TECNICA_UBL_ABC.md) y el
contrato de la API en [`docs/openapi.yaml`](docs/openapi.yaml).

## Requisitos

- .NET SDK 8.0 e Internet la primera vez (NuGet descarga `xunit` para las pruebas).

## Clonar, compilar y probar

```bash
git clone <url-del-repositorio>
cd <carpeta>
dotnet test        # 153 pruebas unitarias xUnit
```

## Qué hay para el Taller 2

| Requisito | Dónde |
|:---|:---|
| Generar el XML **a partir del `UBL-Invoice-2.1.xsd`** de OASIS | Las clases se generan desde el XSD con [`tools/ABC.Billing.GenerarClasesUbl`](tools/README.md) → `src/ABC.Billing.Infrastructure/UblEngine/Generado/UblInvoice21.generated.cs`. El adaptador `GeneradorXmlUblFactura21` las llena y serializa; el XML se valida contra el mismo XSD |
| En el **lenguaje del dominio** | El dominio (`src/ABC.Billing.Core`) habla de `Comprobante`, `LineaComprobante`, `SubtotalTributo`, `Cliente`, `Emisor`; no conoce XML |
| **Pruebas unitarias de generación** | `tests/ABC.Billing.Tests/Generacion/` (estructura, catálogos, importes, escape, ISO-8859-1, XSD, firma, fidelidad con el XSD) |

Ejemplos de salida (firmados y válidos contra el XSD): [`docs/ejemplos`](docs/ejemplos).

## Ejecutar la API

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5099 dotnet run --project src/ABC.Billing.Presentation.Api
```

En Desarrollo los tokens son `dev-emisor-token` (rol emisor) y `dev-admin-token` (rol admin, para la migración).
Fuera de Desarrollo configura `Auth__EmisorToken` y `Auth__AdminToken` (la API no arranca sin ellos).

```bash
curl -s -H "Authorization: Bearer dev-emisor-token" -H "Content-Type: application/json" \
  -d '{"tipoComprobante":"01","cliente":{"tipoDocumento":"6","numeroDocumento":"20123456786","razonSocial":"BODEGA EL ÑANDÚ S.A.C."},"items":[{"descripcion":"Arroz 5 kg","unidadMedida":"NIU","cantidad":10,"valorUnitario":18.00,"afectacionIgv":"10"}]}' \
  http://127.0.0.1:5099/api/v1/invoices
```

## Qué no está verificado

- El envío real al servicio beta de SUNAT (el cliente SOAP se probó con un CDR de ejemplo).
- El cliente MAUI y los empaquetados MSI/APK (son esqueletos).
- Notas de crédito/débito, baja, resumen diario, ISC sistema 03 e IVAP: están en el contrato, no en el código.
