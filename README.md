# medio_curso_arquitectura
examen de medio curso de arquitectura de software

Arquitectura de Software: Sistema de Facturación Electrónica UBL 2.1 (Spec-First)
Caso Práctico de Examen — ABC Software Corp

Este repositorio contiene la entrega formal del Esqueleto de Especificaciones Técnicas (Specs) y diseño de arquitectura para el examen de medio curso de Arquitectura de Software. El trabajo ha sido elaborado mediante la metodología Spec-First asistido por Inteligencia Artificial en el entorno Antigravity IDE.

📌 ¿Qué son los "Specs" en este Proyecto?

Siguiendo las directrices del docente y de la gerencia, se adoptó la metodología Spec-First (Especificación Primero). En lugar de escribir código a ciegas, se diseñó primero el contrato de datos, las validaciones fiscales y las interfaces:

El Principio Clave: En Perú y SUNAT, la data de negocio se expresa en términos locales como serie_numero (ejemplo: F001-00000001) o numero_documento (RUC). En el estándar internacional OASIS UBL 2.1, ese mismo dato viaja estrictamente como la etiqueta XML <cbc:ID>.
El propósito de los Specs es definir este contrato de traducción formal antes de codificar la lógica del sistema.

📂 Documento Principal de Entrega

La especificación técnica exhaustiva con todos los diagramas y matrices se encuentra en:
👉 ESPECIFICACION_TECNICA_UBL_ABC.md

📑 Resumen de los 4 Entregables del Examen
1. Modelo de Dominio y Contratos (DTOs / Schemas)
«Nuestra Data» y Migración del Legado: Diagnóstico de los 5 campos informales (fecha, ingreso, salida, producto, nro_documento) y su mapeo al nuevo modelo normalizado sin pérdida de trazabilidad contable.
Diccionario de Datos: Tipos SQL, longitudes, restricciones y validaciones para cada campo.
JSON Schema / DTOs: Contrato estricto para la creación de facturas con validación de RUC (Módulo 11).
2. Diagramas de Arquitectura (MVC con visión a Hexagonal)
Monolito Limpio Inicial: Patrón MVC para separación nítida entre interfaz, lógica y almacenamiento.
Evolución Hexagonal (Ports & Adapters): Lógica tributaria aislada en el núcleo; base de datos, serializadores XML y Web Services de SUNAT tratados como adaptadores desacoplados.
Diagrama de Secuencia UML: Flujo cronológico numerado del comprobante (Cajero 
→
→ Controlador MVC 
→
→ Dominio 
→
→ UBL 2.1 
→
→ Firma XAdES 
→
→ SOAP OSE/SUNAT 
→
→ Recepción del CDR).
Estructura de Carpetas: Árbol físico de directorios del código fuente que refleja los puertos y adaptadores.
3. Especificación de Endpoints / Servicios (Contratos API)
Definición de contratos bajo estándar OpenAPI 3.0:
POST /api/v1/invoices (Crear y validar comprobante).
POST /api/v1/invoices/{id}/xml (Generar XML UBL 2.1).
POST /api/v1/invoices/{id}/transmit (Firmar y enviar a OSE/SUNAT).
POST /api/v1/migration/legacy-stock (Ingesta de movimientos históricos).
4. Matriz de Trazabilidad Normativa (SUNAT vs UBL 2.1)
Cruce campo por campo entre las resoluciones de la SUNAT (RS Nº 097-2012 / RS Nº 234-2006) y el esquema UBL-Invoice-2.1.xsd.
Traducción Fundamental: 
SUNAT (Per
u
ˊ
): 
𝑠
𝑒
𝑟
𝑖
𝑒
_
𝑛
𝑢
𝑚
𝑒
𝑟
𝑜
⟷
OASIS UBL 2.1: 
⟨
𝑐
𝑏
𝑐
:
𝐼
𝐷
⟩
SUNAT (Per
u
ˊ
): serie_numero⟷OASIS UBL 2.1: ⟨cbc:ID⟩
Catálogo 08 de SUNAT (Punto Crítico): Códigos de tributos desagregados:
1000 
→
→ IGV (18% - Operación Onerosa).
7152 
→
→ ICBPER (Impuesto a las bolsas plásticas en venta de abarrotes).
9997 / 9998 
→
→ Operaciones Exoneradas e Inafectas.
📱 Distribución Multiplataforma
Tecnología Seleccionada: .NET MAUI (Multi-platform App UI).
Entregables de Empaquetado:
Windows Desktop: Instalador nativo .msi para cajas de cobro fijas en tienda.
Android Mobile: Paquete instalador .apk para terminales POS portátiles en almacén.
Ventaja Arquitectónica: 100% de la lógica de dominio y validaciones fiscales se comparten sin duplicar código.
