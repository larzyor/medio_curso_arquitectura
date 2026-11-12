<?xml version="1.0" encoding="UTF-8"?>
<!-- Reimplementación mínima de commons/error/validate_utils.xsl (IBM DataPower, no incluido en el ZIP de SUNAT).
     Cada plantilla emite <e code="..." kind="ERR|OBS" path="..."/> en lugar de lanzar el error. -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:regexp="http://exslt.org/regular-expressions">

  <xsl:template name="emit"><xsl:param name="code"/><xsl:param name="node" select="/*"/><xsl:param name="desc" select="''"/>
    <e code="{$code}" desc="{$desc}">
      <xsl:attribute name="kind"><xsl:choose><xsl:when test="number($code) &gt;= 4000">OBS</xsl:when><xsl:otherwise>ERR</xsl:otherwise></xsl:choose></xsl:attribute>
      <xsl:attribute name="at"><xsl:for-each select="$node[1]/ancestor-or-self::*"><xsl:value-of select="concat('/',name())"/></xsl:for-each></xsl:attribute>
    </e>
  </xsl:template>

  <xsl:template name="existElement"><xsl:param name="errorCodeNotExist"/><xsl:param name="node"/><xsl:param name="isError" select="true()"/>
    <xsl:if test="not($node)"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeNotExist"/><xsl:with-param name="desc" select="'no existe el tag'"/></xsl:call-template></xsl:if>
  </xsl:template>

  <xsl:template name="existElementNoVacio"><xsl:param name="errorCodeNotExist"/><xsl:param name="node"/><xsl:param name="isError" select="true()"/>
    <xsl:if test="not($node) or normalize-space(string($node[1]))=''"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeNotExist"/><xsl:with-param name="desc" select="'tag inexistente o vacío'"/></xsl:call-template></xsl:if>
  </xsl:template>

  <xsl:template name="regexpValidateElementIfExist"><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="regexp"/><xsl:param name="isError" select="true()"/><xsl:param name="descripcion" select="''"/>
    <xsl:if test="$node and not(regexp:test(string($node[1]), string($regexp)))"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="desc" select="concat('valor «',string($node[1]),'» no cumple ',$regexp,' ',$descripcion)"/></xsl:call-template></xsl:if>
  </xsl:template>

  <xsl:template name="existAndRegexpValidateElement"><xsl:param name="errorCodeNotExist"/><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="regexp"/><xsl:param name="isError" select="true()"/><xsl:param name="descripcion" select="''"/>
    <xsl:choose>
      <xsl:when test="not($node)"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeNotExist"/><xsl:with-param name="desc" select="'no existe el tag'"/></xsl:call-template></xsl:when>
      <xsl:when test="not(regexp:test(string($node[1]), string($regexp)))"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="desc" select="concat('valor «',string($node[1]),'» no cumple ',$regexp,' ',$descripcion)"/></xsl:call-template></xsl:when>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="isTrueExpresion"><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="expresion"/><xsl:param name="isError" select="true()"/><xsl:param name="descripcion" select="''"/>
    <xsl:if test="$expresion"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="desc" select="concat('regla de coherencia incumplida ',$descripcion)"/></xsl:call-template></xsl:if>
  </xsl:template>

  <xsl:template name="isTrueExpresionIfExist"><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="expresion"/><xsl:param name="isError" select="true()"/><xsl:param name="descripcion" select="''"/>
    <xsl:if test="$node and $expresion"><xsl:call-template name="emit"><xsl:with-param name="code" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="desc" select="concat('regla de coherencia incumplida ',$descripcion)"/></xsl:call-template></xsl:if>
  </xsl:template>

  <!-- decimales: número con hasta N decimales; isGreaterCero=true exige &gt; 0 -->
  <xsl:template name="decimalOk"><xsl:param name="v"/><xsl:param name="max"/><xsl:param name="gt0"/>
    <xsl:choose>
      <xsl:when test="not(regexp:test(string($v), concat('^[0-9]+(\.[0-9]{1,',$max,'})?$')))">0</xsl:when>
      <xsl:when test="$gt0 and number($v) &lt;= 0">0</xsl:when>
      <xsl:otherwise>1</xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="existAndValidateValueTwoDecimal"><xsl:param name="errorCodeNotExist"/><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="isGreaterCero" select="false()"/><xsl:param name="descripcion" select="''"/>
    <xsl:call-template name="chkDec"><xsl:with-param name="c1" select="$errorCodeNotExist"/><xsl:with-param name="c2" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="max" select="2"/><xsl:with-param name="gt0" select="$isGreaterCero"/><xsl:with-param name="req" select="true()"/></xsl:call-template>
  </xsl:template>
  <xsl:template name="validateValueTwoDecimalIfExist"><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="isGreaterCero" select="false()"/><xsl:param name="descripcion" select="''"/>
    <xsl:call-template name="chkDec"><xsl:with-param name="c2" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="max" select="2"/><xsl:with-param name="gt0" select="$isGreaterCero"/><xsl:with-param name="req" select="false()"/></xsl:call-template>
  </xsl:template>
  <xsl:template name="existAndValidateValueTenDecimal"><xsl:param name="errorCodeNotExist"/><xsl:param name="errorCodeValidate"/><xsl:param name="node"/><xsl:param name="isGreaterCero" select="false()"/><xsl:param name="descripcion" select="''"/>
    <xsl:call-template name="chkDec"><xsl:with-param name="c1" select="$errorCodeNotExist"/><xsl:with-param name="c2" select="$errorCodeValidate"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="max" select="10"/><xsl:with-param name="gt0" select="$isGreaterCero"/><xsl:with-param name="req" select="true()"/></xsl:call-template>
  </xsl:template>
  <xsl:template name="chkDec"><xsl:param name="c1"/><xsl:param name="c2"/><xsl:param name="node"/><xsl:param name="max"/><xsl:param name="gt0"/><xsl:param name="req"/>
    <xsl:choose>
      <xsl:when test="not($node)"><xsl:if test="$req"><xsl:call-template name="emit"><xsl:with-param name="code" select="$c1"/><xsl:with-param name="desc" select="'no existe el tag'"/></xsl:call-template></xsl:if></xsl:when>
      <xsl:otherwise>
        <xsl:variable name="ok"><xsl:call-template name="decimalOk"><xsl:with-param name="v" select="string($node[1])"/><xsl:with-param name="max" select="$max"/><xsl:with-param name="gt0" select="$gt0"/></xsl:call-template></xsl:variable>
        <xsl:if test="$ok='0'"><xsl:call-template name="emit"><xsl:with-param name="code" select="$c2"/><xsl:with-param name="node" select="$node"/><xsl:with-param name="desc" select="concat('importe «',string($node[1]),'» inválido (máx. ',$max,' decimales)')"/></xsl:call-template></xsl:if>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <!-- Catálogos: SUNAT los lee de archivos internos que no se publican; aquí se REGISTRAN como no verificados -->
  <xsl:template name="findElementInCatalog"><xsl:param name="errorCodeValidate"/><xsl:param name="idCatalogo"/><xsl:param name="catalogo"/><xsl:param name="descripcion" select="''"/>
    <skip kind="catalogo" code="{$errorCodeValidate}" catalogo="{$catalogo}" valor="{string($idCatalogo[1])}"/>
  </xsl:template>
  <xsl:template name="findElementInCatalogProperty"><xsl:param name="errorCodeValidate"/><xsl:param name="idCatalogo"/><xsl:param name="catalogo"/><xsl:param name="propiedad"/><xsl:param name="valorPropiedad"/>
    <skip kind="catalogo-prop" code="{$errorCodeValidate}" catalogo="{$catalogo}" valor="{string($idCatalogo[1])}"/>
  </xsl:template>
</xsl:stylesheet>
