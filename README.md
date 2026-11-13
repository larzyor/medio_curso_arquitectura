# ABC Billing: facturación electrónica UBL 2.1 / SUNAT

## Descripción del caso

**ABC Software Corp** es una empresa mediana que comercializa abarrotes y mercadería al por mayor y menor. Su software
actual solo controla fichas sencillas del almacén (`fecha`, `ingreso`, `salida`, `producto`, `nro_documento`). SUNAT le
exige emitir comprobantes electrónicos en XML bajo el estándar **UBL**, firmados digitalmente, y el sistema actual no valida
RUC, no calcula bien IGV, ISC ni ICBPER, no genera XML firmados y no se comunica con un OSE o con SUNAT.

El equipo aplica **Spec-First**: primero la especificación (reglas, contratos, validaciones) y después el código. Este
repositorio contiene ambas cosas. En el **Taller 2 (2.º parcial)** el caso se concreta en **generar el XML de la factura
electrónica a partir del esquema oficial `UBL-Invoice-2.1.xsd`**, con pruebas unitarias de generación.

La descripción completa, la especificación técnica y el contrato de la API están en
[`docs/ESPECIFICACION_TECNICA_UBL_ABC.md`](docs/ESPECIFICACION_TECNICA_UBL_ABC.md) (sección 0) y
[`docs/openapi.yaml`](docs/openapi.yaml).

## Qué hay para el Taller 2

| Requisito | Dónde |
|:---|:---|
| Generar el XML **a partir del `UBL-Invoice-2.1.xsd`** de OASIS | Las clases se generan desde el XSD con [`tools/ABC.Billing.GenerarClasesUbl`](tools/README.md) → `src/ABC.Billing.Infrastructure/UblEngine/Generado/UblInvoice21.generated.cs`. El adaptador `GeneradorXmlUblFactura21` las llena y serializa; el XML se valida contra el mismo XSD |
| **En el lenguaje que sepan** (aclaración del profesor) | **C# (.NET 8)**. Además, el dominio (`src/ABC.Billing.Core`) habla de `Comprobante`, `LineaComprobante`, `SubtotalTributo`, `Cliente` y `Emisor`, y no conoce XML |
| **Pruebas unitarias de generación** | `tests/ABC.Billing.Tests/Generacion/` (estructura, catálogos, importes, escape, ISO-8859-1, XSD, firma, fidelidad con el XSD y el ejemplo C de la guía de SUNAT) |

Ejemplos de salida (firmados y válidos contra el XSD): [`docs/ejemplos`](docs/ejemplos).

## Interfaz web: ingresar datos y obtener el XML

La API sirve una página (`src/ABC.Billing.Presentation.Api/wwwroot/index.html`) donde se ingresan el cliente y los ítems.
Al pulsar **Generar XML** el sistema calcula los impuestos, asigna serie y correlativo, genera el XML UBL 2.1 a partir del
XSD, lo firma y lo valida contra el XSD. El XML aparece en pantalla con los botones **Copiar** y **Descargar**.

```bash
dotnet run --project src/ABC.Billing.Presentation.Api
# abrir http://localhost:5099
```

Para validarlo en [probar-xml.nubefact.com](https://probar-xml.nubefact.com): pegar el XML, **marcar «Firmar»** y pulsar ENVIAR.
En Desarrollo el RUC emisor es el RUC de demostración de nubefact (`20600695771`); con el RUC ficticio del ejemplo, nubefact
responde 2010 (contribuyente no activo). El contador de series vive en memoria y vuelve a 1 al reiniciar la API; como el demo de nubefact recuerda cada serie-número, repetir `F001-1` con otros datos da el error 1033. Por eso la página sugiere una serie al azar (editable). La página es solo una capa de presentación: usa el mismo endpoint `POST /api/v1/invoices`.

## Clonar, compilar y probar

Requisitos: **.NET SDK 8** (o uno más nuevo) e Internet la primera vez, porque NuGet descarga `xunit`.

```bash
git clone <url-del-repositorio>
cd <carpeta>
dotnet test        # 176 pruebas unitarias xUnit
```

## Si algo falla

| Síntoma | Causa y solución |
|:---|:---|
| Windows: `Una directiva de Control de aplicaciones bloqueó este archivo (0x800711C7)` | **Smart App Control** bloquea los DLL recién compilados porque no están firmados. Ejecutar las pruebas en **WSL o Linux**, o desactivar esa función de Windows (en algunas versiones no se puede volver a activar) |
| Linux: `Couldn't find a valid ICU package` | `sudo apt install -y libicu-dev`, o `export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` |
| Ubuntu: `Unable to locate package dotnet-sdk-8.0` | Instalar con el script de Microsoft: `wget https://dot.net/v1/dotnet-install.sh && chmod +x dotnet-install.sh && ./dotnet-install.sh --channel 8.0` y agregar `~/.dotnet` al `PATH` |
| `Unable to load the service index` (NU1301) | No hay Internet, o una red bloquea NuGet |
| Solo hay .NET 10 instalado | El proyecto es .NET 8 (salió de soporte el 10/11/2026) y `Directory.Build.props` activa `RollForward=Major` para que compile y corra en un runtime más nuevo. No se probó con .NET 10 |

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
- La interfaz web se probó con un servidor simulado (formulario, copia del XML, tildes); falta confirmar con la API real en cada máquina.
- El cliente MAUI y los empaquetados MSI/APK (son esqueletos).
- Notas de crédito/débito, baja, resumen diario, descuentos, ISC sistema 03 e IVAP: están en el contrato, no en el código.
