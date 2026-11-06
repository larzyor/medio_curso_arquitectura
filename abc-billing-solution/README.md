# ABC Billing: facturación electrónica UBL 2.1 / SUNAT (versión corregida)

Código de referencia de la especificación Spec-First. **La especificación está en
[`docs/ESPECIFICACION_TECNICA_UBL_ABC.md`](docs/ESPECIFICACION_TECNICA_UBL_ABC.md)** y el contrato de la API en
[`docs/openapi.yaml`](docs/openapi.yaml).

## Requisitos

- .NET SDK 8.0. El código de producción no usa paquetes NuGet de terceros; solo el proyecto de pruebas unitarias usa xUnit (desde nuget.org).
- Opcional: Python 3, solo para regenerar las clases UBL si cambia el XSD.

## Probar

```bash
# 64 comprobaciones: dominio, XML contra el XSD oficial, firma, CDR, migración, concurrencia
dotnet run --project tests/ABC.Billing.SmokeTests docs/ejemplos
```

## Taller 2: XML desde el XSD

Las clases UBL se generan desde `UBL-Invoice-2.1.xsd` y la traducción está escrita en el lenguaje del dominio.
Detalle en [`docs/TALLER_2_XML_DESDE_XSD.md`](docs/TALLER_2_XML_DESDE_XSD.md).

```bash
dotnet test tests/ABC.Billing.UblEngine.Tests              # pruebas unitarias de generación del XML
python tools/UblCodeGen/generar_modelo_ubl.py --check       # el modelo generado sigue igual al XSD
```

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
