# Verificación del XML con las reglas de validación de SUNAT

Ejecuta las reglas oficiales `ValidaExprReg*.xsl` (paquete "XSL actualizado al 06/09/2022" que entregó el profesor) sobre un XML generado
y, aparte, lo valida contra el XSD de UBL 2.1.

## Qué hace y qué NO hace
- SUNAT corre esas reglas en IBM DataPower y usa unas plantillas internas (`commons/error/validate_utils.xsl`) que **no se publican**.
  `validate_utils_stub.xsl` las reimplementa: existencia de etiquetas, expresiones regulares, decimales y expresiones de coherencia.
- Las reglas que consultan **catálogos** no se evalúan: se listan como "no verificadas".
- No consulta el padrón de contribuyentes ni reemplaza al validador en línea (probar-xml.nubefact.com).
- Se adapta el XSL en memoria a dos tolerancias de DataPower que libxslt no admite (variables locales repetidas y variables usadas fuera de alcance); no se cambia ninguna regla.

## Uso (Python 3 con `lxml`: `pip install lxml`)
```bash
# 1) copiar la carpeta "validaciones" del ZIP del profesor aquí, como xsl_sunat/
python3 run_check.py xsl_sunat/ValidaExprRegFactura-2.0.1.xsl ../../docs/ejemplos/factura-ejemplo-firmada.xml 20600000013-01-F001-1.xml
python3 run_check.py xsl_sunat/ValidaExprRegBoleta-2.0.1.xsl  ../../docs/ejemplos/boleta-ejemplo-firmada.xml  20600000013-03-B001-1.xml
python3 xsd_check.py ../../src/ABC.Billing.Infrastructure/UblEngine/Schemas/maindoc/UBL-Invoice-2.1.xsd ../../docs/ejemplos/factura-ejemplo-firmada.xml
```
El tercer argumento es el nombre de archivo con el que se enviaría el XML (RUC-tipo-serie-correlativo): algunas reglas lo comparan con el contenido.
Resultado esperado para los ejemplos del repo: `Errores: 0   Observaciones: 0`.
