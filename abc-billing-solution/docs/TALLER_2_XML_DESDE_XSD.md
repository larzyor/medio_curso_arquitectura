# Taller 2: generar el XML de la factura electrónica desde el XSD

## El encargo

1. Generar el XML de la factura electrónica a partir del esquema oficial `UBL-Invoice-2.1.xsd` de OASIS, el estándar que usa SUNAT.
2. Hacerlo en el lenguaje del dominio: el código habla de comprobantes, líneas, tributos y clientes, no de etiquetas XML.
3. Demostrarlo con pruebas unitarias de generación.

## Antes y después

| | Antes | Ahora |
|:---|:---|:---|
| Clases UBL | 25 clases escritas a mano (`Model/UblInvoiceModel.cs`) que copiaban un subconjunto del XSD | **124 clases generadas** desde el XSD (`Generated/UblInvoice21.g.cs`): 110 agregados `cac:`/`ext:` y 14 tipos de dato `udt:` |
| Orden de los elementos | `Order = 1, 2, 3...` escrito a mano | Sale del `xsd:sequence` del esquema |
| Mapeo | Un método `Mapear()` de 170 líneas lleno de nombres UBL | `ComprobanteUbl`: `.EmitidoPor(emisor).ParaElCliente(cliente).ConTributos(...).ConLinea(linea)` |
| Pruebas | Solo las pruebas de humo (`SmokeTests`) | Proyecto xUnit con pruebas unitarias de generación |

## 1. Del XSD a las clases

```bash
python tools/UblCodeGen/generar_modelo_ubl.py           # regenera UblEngine/Generated/UblInvoice21.g.cs
python tools/UblCodeGen/generar_modelo_ubl.py --check   # falla si el .g.cs ya no coincide con el XSD
```

El generador hace lo mismo que `xsd.exe` o `XmlSchemaClassGenerator`, pero sin paquetes NuGet (el código de producción no usa ninguno) y solo con la biblioteca estándar de Python:

1. Abre `Schemas/maindoc/UBL-Invoice-2.1.xsd` y sigue sus `xsd:import` hacia `common/` (CAC, CBC, UDT, CCTS, extensiones).
2. Parte del elemento raíz `<Invoice>` y recorre solo los tipos alcanzables desde él.
3. Por cada `complexType` con `xsd:sequence` escribe una clase cuyos elementos siguen el orden del XSD (`[XmlElement(..., Order = n)]`). Usa `List<T>` cuando `maxOccurs="unbounded"`.
4. Cada `cbc:*` deriva de un tipo no calificado (`cbc:IDType` → `udt:IdentifierType`). La propiedad usa esa clase udt, que tiene los atributos del XSD (`schemeID`, `currencyID`, `listURI`, `unitCode`...).
5. `ext:ExtensionContent` es un `xsd:any`: queda como `XmlElement Any`, que es donde el firmador inserta `ds:Signature`.

El archivo generado se guarda en el repositorio, así que Python no hace falta para compilar. Solo se usa si cambia el XSD.

## 2. El lenguaje del dominio (`UblEngine/Dominio`)

```csharp
// UblInvoice21XmlSerializer (adaptador del puerto IUblXmlGenerator)
public byte[] GenerarXmlUbl21(Invoice invoice) => ComprobanteUbl.Desde(invoice).ComoXml();

// ComprobanteUbl.Desde(...) cuenta el comprobante así:
Nuevo(comprobante.TipoComprobante, comprobante.SerieNumero, comprobante.FechaEmision, comprobante.Moneda)
    .DeTipoDeOperacion(comprobante.TipoOperacion)
    .ConLeyenda(Leyenda.MontoEnLetras, MontoEnLetras.Convertir(...))
    .EmitidoPor(comprobante.Emisor)
    .ParaElCliente(comprobante.Cliente)
    .ConTributos(comprobante.TotalTributos, comprobante.Tributos)
    .ConTotales(valorVenta, precioVenta, importeAPagar)
    .ConLinea(linea);   // por cada ítem
```

| Clase | Responsabilidad |
|:---|:---|
| `ComprobanteUbl` | El comprobante: encabezado, leyendas, emisor/firmante, cliente, resumen de tributos, totales, líneas y la salida en ISO-8859-1 |
| `LineaUbl` | Una línea con sus tributos en el orden de cálculo: `ConIscSiCorresponde()` → `ConIgvSegunSuAfectacion()` → `ConIcbperSiEsBolsaPlastica()` |
| `VocabularioUbl` | Cómo se escribe cada valor del dominio en UBL: `Importe`, `Cantidad`, `CodigoDeCatalogo`, `DocumentoDeIdentidad`, `CategoriaTributaria`. Es el **único** lugar que conoce los atributos y el formato numérico |

| En el dominio | En UBL 2.1 |
|:---|:---|
| Serie-número, fecha y hora de emisión | `cbc:ID`, `cbc:IssueDate`, `cbc:IssueTime` |
| Tipo de comprobante (Catálogo 01) / tipo de operación (Catálogo 51) | `cbc:InvoiceTypeCode` / `cbc:ProfileID` |
| Leyenda (Catálogo 52) | `cbc:Note/@languageLocaleID` |
| Emisor (y firmante) | `cac:AccountingSupplierParty` y `cac:Signature` |
| Cliente | `cac:AccountingCustomerParty` |
| Tributo del Catálogo 05 | `cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme` |
| Afectación al IGV (Catálogo 07) / sistema de ISC (Catálogo 08) | `cbc:TaxExemptionReasonCode` / `cbc:TierRange` |
| Valor de venta, precio de venta, importe a pagar | `cac:LegalMonetaryTotal` |
| Línea (ítem) | `cac:InvoiceLine` |

## 3. Pruebas unitarias de generación

```bash
dotnet test tests/ABC.Billing.UblEngine.Tests
```

Las pruebas describen el comprobante en el lenguaje del dominio y no escriben XML a mano:

```csharp
var xml = UnComprobante.Factura().ConLineaAfectaAlIscAlValor("Cerveza 620 ml", 6m, 5.00m, tasaIsc: 10m).Xml();
```

| Archivo | Qué demuestra |
|:---|:---|
| `ModeloGeneradoDesdeXsdTests` | Leen el XSD oficial con `XmlSchemaSet` y comparan cada clase generada (orden, namespaces, cardinalidad, tipo udt, atributos). Si alguien edita el `.g.cs` a mano o cambia el XSD sin regenerar, fallan |
| `GeneracionDelComprobanteTests` | Encabezado en el orden del XSD, catálogos 01, 06, 51 y 52, emisor y firmante, cliente con RUC o DNI, moneda, totales y líneas numeradas |
| `TributosEnElXmlTests` | El tributo que corresponde a cada afectación (1000/9997/9998/9995/9996), el ISC antes del IGV y dentro de su base, el ICBPER por bolsa, las entregas gratuitas y el resumen 1000-2000-9997-9998-9996-7152 |
| `CodificacionYEsquemaTests` | ISO-8859-1 (Ñ y Ú en un byte), escape de `& < > "`, punto decimal con cualquier cultura, salida determinista, lectura de vuelta con las mismas clases, y que el XML **firmado** cumple el XSD en 6 escenarios (sin firma lo rechaza) |

Las pruebas de humo de siempre siguen funcionando: `dotnet run --project tests/ABC.Billing.SmokeTests docs/ejemplos`.

## Verificación

- El generador se ejecutó sobre el XSD y el modo `--check` confirma que el archivo generado coincide.
- La factura y la boleta de `docs/ejemplos` (válidas contra el XSD) se recorrieron elemento por elemento contra el modelo generado: todos los elementos y atributos existen en las clases y en el orden correcto.
- **Pendiente de ejecutar en una máquina con el SDK de .NET 8:** `dotnet build` y `dotnet test`. El entorno donde se escribió este taller no tenía el SDK de .NET.
