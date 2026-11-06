#!/usr/bin/env python3
"""
Generador del modelo C# de la factura UBL 2.1 a partir del esquema OFICIAL de OASIS.

    python tools/UblCodeGen/generar_modelo_ubl.py            # regenera el .g.cs
    python tools/UblCodeGen/generar_modelo_ubl.py --check    # falla si el .g.cs no coincide con el XSD

Lee  src/ABC.Billing.Infrastructure/UblEngine/Schemas/maindoc/UBL-Invoice-2.1.xsd, sigue sus xsd:import/xsd:include
y escribe src/ABC.Billing.Infrastructure/UblEngine/Generated/UblInvoice21.g.cs con:

  * una clase por cada complexType de cac:/ext:/Invoice alcanzable desde el elemento raíz <Invoice>,
    con sus elementos en el MISMO ORDEN que el xsd:sequence (atributo Order de XmlSerializer);
  * una clase por cada tipo de dato no calificado (udt:AmountType, udt:CodeType, udt:IdentifierType...) con
    sus atributos (currencyID, listID, schemeID, unitCode...) y el valor como texto. Cada cbc:* usa la clase
    udt de la que deriva (cbc:IDType extiende udt:IdentifierType -> la propiedad es IdentifierType);
  * cardinalidad del XSD: 0..1 / 1..1 -> propiedad; 0..n / 1..n -> List<T>.

Hace lo mismo que xsd.exe o XmlSchemaClassGenerator, pero sin paquetes NuGet (la solución no los usa) y solo con
la biblioteca estándar de Python. No hace falta ejecutarlo para compilar: el .g.cs generado está en el repositorio.
"""
from __future__ import annotations

import os
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field

XSD = "http://www.w3.org/2001/XMLSchema"
CCTS = "urn:un:unece:uncefact:documentation:2"

NS_INVOICE = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
NS_CAC = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
NS_CBC = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
NS_EXT = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"
NS_UDT = "urn:oasis:names:specification:ubl:schema:xsd:UnqualifiedDataTypes-2"
NS_CCT = "urn:un:unece:uncefact:data:specification:CoreComponentTypeSchemaModule:2"

# Nombre de la constante C# para cada namespace (clase UblXmlNs del archivo generado).
CONST_NS = {NS_INVOICE: "Invoice", NS_CAC: "Cac", NS_CBC: "Cbc", NS_EXT: "Ext", NS_UDT: "Udt"}

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ESQUEMA = os.path.join(RAIZ, "src", "ABC.Billing.Infrastructure", "UblEngine", "Schemas", "maindoc", "UBL-Invoice-2.1.xsd")
SALIDA = os.path.join(RAIZ, "src", "ABC.Billing.Infrastructure", "UblEngine", "Generated", "UblInvoice21.g.cs")
NAMESPACE_CS = "ABC.Billing.Infrastructure.UblEngine.Generated"


def q(ns: str, local: str) -> str:
    return f"{{{ns}}}{local}"


# --------------------------------------------------------------------------------------------- lectura del XSD
@dataclass
class Esquema:
    """Todos los esquemas cargados, indexados por (namespace, nombre)."""
    elementos: dict = field(default_factory=dict)       # (ns, nombre) -> (ns_tipo, nombre_tipo, nodo)
    tipos: dict = field(default_factory=dict)           # (ns, nombre) -> nodo xsd:complexType
    cargados: set = field(default_factory=set)


def resolver_qname(texto: str, prefijos: dict, ns_destino: str) -> tuple[str, str]:
    if ":" in texto:
        pre, local = texto.split(":", 1)
        return prefijos[pre], local
    return prefijos.get("", ns_destino), texto


def leer_prefijos(ruta: str) -> dict:
    prefijos = {}
    for evento, (pre, uri) in ET.iterparse(ruta, events=["start-ns"]):
        prefijos.setdefault(pre, uri)
    return prefijos


def cargar(esq: Esquema, ruta: str) -> None:
    ruta = os.path.normpath(ruta)
    if ruta in esq.cargados:
        return
    esq.cargados.add(ruta)
    raiz = ET.parse(ruta).getroot()
    destino = raiz.get("targetNamespace", "")
    prefijos = leer_prefijos(ruta)

    for hijo in raiz:
        if hijo.tag in (q(XSD, "import"), q(XSD, "include")) and hijo.get("schemaLocation"):
            # Los esquemas de firma (xmldsig/XAdES) solo se alcanzan por el xsd:any de ExtensionContent: no generan clases.
            if "Signature" in hijo.get("schemaLocation") or "xmldsig" in hijo.get("schemaLocation"):
                continue
            cargar(esq, os.path.join(os.path.dirname(ruta), hijo.get("schemaLocation")))
        elif hijo.tag == q(XSD, "element"):
            ns_t, nom_t = resolver_qname(hijo.get("type"), prefijos, destino)
            esq.elementos[(destino, hijo.get("name"))] = (ns_t, nom_t, hijo)
        elif hijo.tag == q(XSD, "complexType"):
            hijo.set("{gen}prefijos", repr(prefijos))
            hijo.set("{gen}ns", destino)
            esq.tipos[(destino, hijo.get("name"))] = hijo


def definicion(nodo) -> str:
    """Primera oración de ccts:Definition (se usa como comentario XML en el C#)."""
    d = nodo.find(f"{q(XSD, 'annotation')}/{q(XSD, 'documentation')}/{q(CCTS, 'Component')}/{q(CCTS, 'Definition')}")
    if d is None:
        d = nodo.find(f"{q(XSD, 'annotation')}/{q(XSD, 'documentation')}/{q(CCTS, 'Definition')}")
    if d is None or not (d.text or "").strip():
        return ""
    texto = " ".join(d.text.split())
    if len(texto) > 160:
        texto = texto[:157].rstrip() + "..."
    return texto.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


# --------------------------------------------------------------------------------------------- modelo intermedio
@dataclass
class Miembro:
    nombre_xml: str
    ns_xml: str
    tipo_cs: str
    min: int
    max: str          # "1" o "unbounded"
    doc: str
    nombre_cs: str = ""


@dataclass
class Clase:
    nombre: str
    ns: str
    doc: str
    miembros: list = field(default_factory=list)     # elementos en orden del xsd:sequence
    atributos: list = field(default_factory=list)    # (nombre_xml, requerido) para tipos de valor
    es_valor: bool = False                           # tipo udt (simpleContent)
    any_element: bool = False                        # ext:ExtensionContent (xsd:any)
    raiz: bool = False


def prefijos_de(nodo) -> dict:
    return eval(nodo.get("{gen}prefijos"))  # noqa: S307 (lo escribimos nosotros arriba)


def tipo_udt_de(esq: Esquema, ns: str, nombre: str) -> tuple[str, str]:
    """Sube por la cadena simpleContent/extension|restriction hasta el tipo udt:* del que deriva un cbc:*Type."""
    actual = (ns, nombre)
    while actual[0] != NS_UDT:
        nodo = esq.tipos.get(actual)
        if nodo is None:
            raise SystemExit(f"Tipo no encontrado: {actual}")
        sc = nodo.find(q(XSD, "simpleContent"))
        if sc is None:
            raise SystemExit(f"{actual} no es simpleContent")
        deriv = sc[0]
        if actual[0] == NS_CBC and len(list(deriv.findall(q(XSD, "attribute")))):
            raise SystemExit(f"{actual} agrega atributos propios: el generador asume que no (UBL 2.1 no lo hace)")
        actual = resolver_qname(deriv.get("base"), prefijos_de(nodo), nodo.get("{gen}ns"))
    return actual


def atributos_de(esq: Esquema, ns: str, nombre: str) -> list[tuple[str, bool]]:
    """Atributos efectivos de un tipo de valor: los de la cadena udt -> ccts-cct, sin los prohibidos."""
    cadena = []
    actual = (ns, nombre)
    while actual in esq.tipos:
        nodo = esq.tipos[actual]
        cadena.append(nodo)
        sc = nodo.find(q(XSD, "simpleContent"))
        deriv = sc[0]
        base = resolver_qname(deriv.get("base"), prefijos_de(nodo), nodo.get("{gen}ns"))
        if base[0] == XSD:
            break
        actual = base
    efectivos: dict[str, bool] = {}
    for nodo in reversed(cadena):                       # de la base (cct) hacia el tipo udt
        for a in nodo.find(q(XSD, "simpleContent"))[0].findall(q(XSD, "attribute")):
            uso = a.get("use", "optional")
            if uso == "prohibited":
                efectivos.pop(a.get("name"), None)
            else:
                efectivos[a.get("name")] = uso == "required"
    return list(efectivos.items())


def construir(esq: Esquema) -> list[Clase]:
    clases: dict[tuple[str, str], Clase] = {}
    pendientes = [(NS_INVOICE, "InvoiceType")]

    while pendientes:
        clave = pendientes.pop()
        if clave in clases:
            continue
        nodo = esq.tipos[clave]
        ns, nombre = clave

        if ns == NS_UDT:
            clases[clave] = Clase(nombre, ns, definicion(nodo), atributos=atributos_de(esq, ns, nombre), es_valor=True)
            continue

        clase = Clase(nombre, ns, definicion(nodo), raiz=(clave == (NS_INVOICE, "InvoiceType")))
        seq = nodo.find(q(XSD, "sequence"))
        hijos = list(seq) if seq is not None else []
        for h in hijos:
            if h.tag == q(XSD, "any"):
                clase.any_element = True
                continue
            if h.tag != q(XSD, "element") or h.get("ref") is None:
                raise SystemExit(f"Construcción no soportada en {clave}: {h.tag}")
            ns_el, nom_el = resolver_qname(h.get("ref"), prefijos_de(nodo), ns)
            ns_tipo, nom_tipo = esq.elementos[(ns_el, nom_el)][:2]
            tipo_nodo = esq.tipos[(ns_tipo, nom_tipo)]
            if tipo_nodo.find(q(XSD, "simpleContent")) is not None:
                ns_tipo, nom_tipo = tipo_udt_de(esq, ns_tipo, nom_tipo)
            pendientes.append((ns_tipo, nom_tipo))
            clase.miembros.append(Miembro(
                nom_el, ns_el, nom_tipo,
                int(h.get("minOccurs", "1")), h.get("maxOccurs", "1"),
                definicion(h)))
        clases[clave] = clase

    # Nombres de clase únicos (todas viven en el mismo namespace C#).
    nombres = [c.nombre for c in clases.values()]
    repetidos = {n for n in nombres if nombres.count(n) > 1}
    if repetidos:
        raise SystemExit(f"Nombres de clase repetidos entre namespaces XML: {sorted(repetidos)}")

    # C# no permite un miembro con el mismo nombre que su clase (CS0542): cac:PriceType contiene cbc:PriceType.
    for c in clases.values():
        for m in c.miembros:
            m.nombre_cs = m.nombre_xml + "Value" if m.nombre_xml == c.nombre else m.nombre_xml

    raiz = clases.pop((NS_INVOICE, "InvoiceType"))
    resto = sorted(clases.values(), key=lambda c: (c.es_valor, c.ns != NS_EXT, c.nombre))
    return [raiz] + resto


# --------------------------------------------------------------------------------------------- escritura del C#
def pascal(nombre_xml: str) -> str:
    return nombre_xml[0].upper() + nombre_xml[1:]


def cardinalidad(m: Miembro) -> str:
    return f"{m.min}..{'n' if m.max == 'unbounded' else m.max}"


def escribir(clases: list[Clase]) -> str:
    total_elementos = sum(len(c.miembros) for c in clases)
    o = []
    w = o.append
    w("// <auto-generated>")
    w("//   Generado por tools/UblCodeGen/generar_modelo_ubl.py a partir del esquema OFICIAL de OASIS:")
    w("//   UblEngine/Schemas/maindoc/UBL-Invoice-2.1.xsd (y los módulos common/ que importa).")
    w(f"//   {len(clases)} clases, {total_elementos} elementos. NO EDITAR A MANO: los cambios se pierden al regenerar.")
    w("//   El orden de los elementos (Order) es el del xsd:sequence; la cardinalidad está en cada comentario.")
    w("// </auto-generated>")
    w("#nullable enable")
    w("#pragma warning disable CS1591 // miembros públicos sin comentario (los tipos de valor no lo necesitan)")
    w("")
    w("using System.Collections.Generic;")
    w("using System.Xml.Serialization;")
    w("")
    w(f"namespace {NAMESPACE_CS};")
    w("")
    w("/// <summary>Namespaces XML de UBL 2.1 (tal como los declara el XSD).</summary>")
    w("public static class UblXmlNs")
    w("{")
    for ns, nombre in CONST_NS.items():
        w(f'    public const string {nombre} = "{ns}";')
    w("}")

    for c in clases:
        w("")
        if c.doc:
            w(f"/// <summary>{c.doc}</summary>")
        if c.raiz:
            w(f'[XmlRoot("Invoice", Namespace = UblXmlNs.Invoice)]')
        w(f'[XmlType("{c.nombre}", Namespace = UblXmlNs.{CONST_NS[c.ns]})]')
        w(f"public partial class {c.nombre}")
        w("{")
        if c.es_valor:
            for nombre_xml, requerido in c.atributos:
                req = " (obligatorio)" if requerido else ""
                w(f"    /// <summary>@{nombre_xml}{req}</summary>")
                w(f'    [XmlAttribute("{nombre_xml}")] public string? {pascal(nombre_xml)} {{ get; set; }}')
            w("    /// <summary>Contenido del elemento.</summary>")
            w("    [XmlText] public string Value { get; set; } = \"\";")
        orden = 0
        for m in c.miembros:
            orden += 1
            prefijo = {NS_CAC: "cac", NS_CBC: "cbc", NS_EXT: "ext"}[m.ns_xml]
            doc = f" {m.doc}" if m.doc else ""
            w(f"    /// <summary>{prefijo}:{m.nombre_xml} [{cardinalidad(m)}].{doc}</summary>")
            atributo = f'[XmlElement("{m.nombre_xml}", Namespace = UblXmlNs.{CONST_NS[m.ns_xml]}, Order = {orden})]'
            if m.max == "unbounded":
                w(f"    {atributo} public List<{m.tipo_cs}> {m.nombre_cs} {{ get; set; }} = new();")
            else:
                w(f"    {atributo} public {m.tipo_cs}? {m.nombre_cs} {{ get; set; }}")
        if c.any_element:
            orden += 1
            w("    /// <summary>xsd:any (##other): aquí el firmador inserta ds:Signature.</summary>")
            w(f"    [XmlAnyElement(Order = {orden})] public System.Xml.XmlElement? Any {{ get; set; }}")
        w("}")
    w("")
    return "\n".join(o)


def main() -> int:
    esq = Esquema()
    cargar(esq, ESQUEMA)
    clases = construir(esq)
    codigo = escribir(clases)

    if "--check" in sys.argv:
        actual = open(SALIDA, encoding="utf-8").read() if os.path.exists(SALIDA) else ""
        if actual != codigo:
            print("El modelo generado NO coincide con el XSD: ejecute el generador.", file=sys.stderr)
            return 1
        print("Modelo generado al día con el XSD.")
        return 0

    os.makedirs(os.path.dirname(SALIDA), exist_ok=True)
    with open(SALIDA, "w", encoding="utf-8", newline="\n") as f:
        f.write(codigo)
    valores = sum(1 for c in clases if c.es_valor)
    print(f"{os.path.relpath(SALIDA, RAIZ)}: {len(clases)} clases ({len(clases) - valores} agregados, {valores} tipos de valor), "
          f"{sum(len(c.miembros) for c in clases)} elementos")
    return 0


if __name__ == "__main__":
    sys.exit(main())
