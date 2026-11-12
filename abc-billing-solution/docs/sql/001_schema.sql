-- Esquema propuesto para el adaptador SqlInvoiceRepository (fase 2). PostgreSQL.
-- Los códigos de catálogo se guardan como texto con su código SUNAT (se validan en el dominio).

CREATE TABLE serie_correlativo (
    tipo_comprobante CHAR(2)      NOT NULL,            -- Catálogo 01
    serie            CHAR(4)      NOT NULL CHECK (serie ~ '^[FB][A-Z0-9]{3}$'),
    ultimo           BIGINT       NOT NULL DEFAULT 0 CHECK (ultimo BETWEEN 0 AND 99999999),
    PRIMARY KEY (tipo_comprobante, serie)
);
-- Asignación atómica del siguiente correlativo (una sola sentencia, segura ante concurrencia):
--   UPDATE serie_correlativo SET ultimo = ultimo + 1 WHERE tipo_comprobante = $1 AND serie = $2 RETURNING ultimo;

CREATE TABLE comprobante (
    id                UUID PRIMARY KEY,
    tipo_comprobante  CHAR(2)       NOT NULL,          -- Catálogo 01
    tipo_operacion    CHAR(4)       NOT NULL,          -- Catálogo 51 (cbc:ProfileID)
    serie             CHAR(4)       NOT NULL,
    correlativo       BIGINT        NOT NULL,
    fecha_emision     TIMESTAMP     NOT NULL,
    moneda            CHAR(3)       NOT NULL,          -- Catálogo 02
    cliente_tipo_doc  CHAR(1)       NOT NULL,          -- Catálogo 06
    cliente_nro_doc   VARCHAR(15)   NOT NULL,
    cliente_nombre    VARCHAR(250)  NOT NULL,
    total_gravadas    NUMERIC(14,2) NOT NULL,
    total_exoneradas  NUMERIC(14,2) NOT NULL,
    total_inafectas   NUMERIC(14,2) NOT NULL,
    total_exportacion NUMERIC(14,2) NOT NULL,
    total_gratuitas   NUMERIC(14,2) NOT NULL,
    base_igv          NUMERIC(14,2) NOT NULL,
    total_igv         NUMERIC(14,2) NOT NULL,
    total_isc         NUMERIC(14,2) NOT NULL,
    total_icbper      NUMERIC(14,2) NOT NULL,
    total_valor_venta NUMERIC(14,2) NOT NULL,
    total_precio_venta NUMERIC(14,2) NOT NULL,
    importe_total     NUMERIC(14,2) NOT NULL,
    estado            VARCHAR(30)   NOT NULL,
    xml_firmado       BYTEA,
    digest_value      VARCHAR(100),
    cdr_zip           BYTEA,
    cdr_codigo        VARCHAR(5),
    cdr_descripcion   VARCHAR(500),
    UNIQUE (tipo_comprobante, serie, correlativo)       -- la numeración nunca se reutiliza
);

CREATE TABLE comprobante_item (
    comprobante_id UUID          NOT NULL REFERENCES comprobante(id),
    numero_linea   INT           NOT NULL,
    codigo_producto VARCHAR(30),
    descripcion    VARCHAR(250)  NOT NULL,
    unidad_medida  VARCHAR(3)    NOT NULL,             -- Catálogo 03
    cantidad       NUMERIC(14,4) NOT NULL CHECK (cantidad > 0),
    valor_unitario NUMERIC(16,10) NOT NULL CHECK (valor_unitario > 0),
    afectacion_igv CHAR(2)       NOT NULL,             -- Catálogo 07
    sistema_isc    CHAR(2),                            -- Catálogo 08 (solo ISC)
    tasa_isc       NUMERIC(7,4),
    monto_fijo_isc NUMERIC(14,2),
    aplica_icbper  BOOLEAN       NOT NULL DEFAULT FALSE,
    valor_venta    NUMERIC(14,2) NOT NULL,
    monto_isc      NUMERIC(14,2) NOT NULL,
    base_igv       NUMERIC(14,2) NOT NULL,
    monto_igv      NUMERIC(14,2) NOT NULL,
    monto_icbper   NUMERIC(14,2) NOT NULL,
    PRIMARY KEY (comprobante_id, numero_linea)
);

CREATE TABLE comprobante_tributo (
    comprobante_id UUID          NOT NULL REFERENCES comprobante(id),
    codigo_tributo CHAR(4)       NOT NULL,             -- Catálogo 05 (1000, 2000, 7152, 9996, 9997...)
    base_imponible NUMERIC(14,2),
    monto          NUMERIC(14,2) NOT NULL,
    PRIMARY KEY (comprobante_id, codigo_tributo)
);

-- Migración del sistema legado: kardex formal + auditoría con los 5 campos originales.
CREATE TABLE kardex_movimiento (
    id          UUID PRIMARY KEY,
    fecha       DATE          NOT NULL,
    sku         VARCHAR(20)   NOT NULL,
    descripcion VARCHAR(250)  NOT NULL,
    unidad_medida VARCHAR(3)  NOT NULL DEFAULT 'NIU',  -- Catálogo 03
    entrada     NUMERIC(14,4) NOT NULL DEFAULT 0 CHECK (entrada >= 0),
    salida      NUMERIC(14,4) NOT NULL DEFAULT 0 CHECK (salida >= 0),
    concepto    VARCHAR(40)   NOT NULL                 -- p. ej. SALDO_INICIAL_MIGRACION
);

CREATE TABLE migracion_legado_auditoria (
    fila            INT           NOT NULL,
    fecha           DATE          NOT NULL,
    ingreso         NUMERIC(14,4) NOT NULL,
    salida          NUMERIC(14,4) NOT NULL,
    producto_original VARCHAR(250) NOT NULL,
    nro_documento   VARCHAR(60),                       -- nro_documento del software básico
    sku             VARCHAR(20)   NOT NULL,
    kardex_movimiento_id UUID     NOT NULL REFERENCES kardex_movimiento(id)
);
