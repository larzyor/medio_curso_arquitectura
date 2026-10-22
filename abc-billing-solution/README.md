# ABC Software Corp — Facturación Electrónica UBL 2.1 (Spec-First)

Este proyecto implementa la arquitectura de software y el esqueleto de especificaciones técnicas (**Spec-First**) para la emisión de comprobantes electrónicos con valor legal ante la **SUNAT** (Perú) bajo el estándar **OASIS UBL 2.1**, así como la migración y trazabilidad del software de almacén informal legado.

---

## 🏗️ Arquitectura de la Solución (MVC Desacoplado a Hexagonal)

La solución sigue un diseño modular donde el **Monolito MVC** organiza las capas externas, mientras que el interior está desacoplado mediante **Puertos y Adaptadores (Arquitectura Hexagonal)**:

```
abc-billing-solution/
├── src/
│   ├── ABC.Billing.Core/                    # Dominio puro y Puertos de entrada/salida
│   │   ├── Domain/                          # Entidades (Invoice, Customer), Value Objects (RucNumber con Módulo 11)
│   │   └── Ports/                           # Contratos (IInvoiceRepository, IUblXmlGenerator, ISunatOseClient)
│   ├── ABC.Billing.Infrastructure/          # Adaptadores (UblInvoice21XmlSerializer, Firma XAdES, SOAP SUNAT)
│   ├── ABC.Billing.Presentation.Api/        # Controladores MVC y DTOs ('serie_numero')
│   └── ABC.Billing.App.Client/              # Vistas multiplataforma (.NET MAUI)
├── packaging/
│   ├── windows-msi/                         # Configuración WiX Toolset para instalador nativo MSI
│   └── android-apk/                         # Configuración Gradle para paquete APK Android
└── docs/                                    # Especificaciones técnicas completas y normativas
```

---

## 🔑 Puntos Clave del Examen

1. **Equivalencia de Identificadores (SUNAT vs UBL 2.1):**
   * En Perú / SUNAT: **`serie_numero`** (ej: `F001-00000001`).
   * En OASIS UBL 2.1: **`<cbc:ID>`** (`UBL-Invoice-2.1.xsd`).
2. **Catálogo 08 de SUNAT (Tributos):**
   * Código `1000` = IGV (18%).
   * Código `7152` = ICBPER (Impuesto a las bolsas plásticas en abarrotes).
   * Código `9997` / `9998` = Operaciones Exoneradas e Inafectas.
3. **Validación Invariante de RUC:**
   * Algoritmo Módulo 11 implementado en el Value Object `RucNumber`.
4. **Distribución Multiplataforma:**
   * Instalador nativo **`.msi`** para Windows Desktop.
   * Paquete **`.apk`** para terminales POS Android.
