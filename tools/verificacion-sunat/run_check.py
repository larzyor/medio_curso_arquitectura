"""Ejecuta las reglas XSL de SUNAT (ValidaExprReg*.xsl) sobre un XML, con un sustituto de las plantillas de DataPower.
Uso: python3 run_check.py <xsl> <xml> [nombre_archivo_envio]"""
import sys, re, os, collections
from lxml import etree
xsl_path, xml_path = sys.argv[1], sys.argv[2]
nombre = sys.argv[3] if len(sys.argv) > 3 else os.path.basename(xml_path)
here = os.path.dirname(os.path.abspath(__file__))
src = open(xsl_path, encoding="utf-8").read()
src = src.replace("local:///commons/error/validate_utils.xsl", "file://" + os.path.join(here, "validate_utils_stub.xsl"))
src = src.replace("local:///commons/cpe/datos/dat_fechas.xml", "file://" + os.path.join(here, "dat_fechas.xml"))
ns = etree.FunctionNamespace("http://www.datapower.com/extensions")
ns["variable"] = lambda ctx, name: nombre if "nombreArchivoEnviado" in str(name) else ""
XSL = "http://www.w3.org/1999/XSL/Transform"
def preparar(src, extra_globales):
    """libxslt es estricto con dos cosas que DataPower tolera: variables locales repetidas en una misma plantilla
    y variables usadas fuera de su alcance (alcance dinámico). Se corrigen sin cambiar ninguna regla."""
    root = etree.fromstring(src.encode("utf-8"), base_url=xsl_path)
    for tpl in root.iter("{%s}template" % XSL):
        vistos = {}
        for el in tpl.iter("{%s}variable" % XSL):
            n = el.get("name")
            if n in vistos:
                vistos[n] += 1
                nuevo = f"{n}__{vistos[n]}"
                el.set("name", nuevo)
                # reescribir referencias posteriores (hermanos siguientes y sus descendientes)
                pat = re.compile(r"\$" + re.escape(n) + r"(?![\w\-.])")
                for sib in el.itersiblings():
                    for x in sib.iter():
                        for k, v in list(x.attrib.items()):
                            if "$" in v: x.set(k, pat.sub("$" + nuevo, v))
            else:
                vistos[n] = 1
    for g in extra_globales:
        v = etree.Element("{%s}variable" % XSL, name=g, select="''")
        root.insert(0, v)
    return root

globales = []
for _ in range(40):
    try:
        xslt = etree.XSLT(preparar(src, globales))
        doc = etree.parse(xml_path)
        res = xslt(doc)
        out = etree.fromstring("<r>" + re.sub(r'<\?xml[^>]*\?>', '', str(res)) + "</r>")
        break
    except etree.XSLTApplyError:
        msgs = [l.message for l in (xslt.error_log if 'xslt' in dir() else [])]
        m = next((re.search(r"Variable '([^']+)' has not been declared", x) for x in msgs if "has not been declared" in x), None)
        if not m:
            for x in msgs: print("XSLT:", x)
            raise SystemExit(2)
        globales.append(m.group(1))


errs = [e for e in out.iter("e")]
skips = [e for e in out.iter("skip")]
print(f"XML: {os.path.basename(xml_path)}   (nombre de envío simulado: {nombre})")
print(f"Errores: {sum(e.get('kind')=='ERR' for e in errs)}   Observaciones: {sum(e.get('kind')=='OBS' for e in errs)}   Catálogos sin verificar: {len(skips)}")
for e in errs:
    print(f"  [{e.get('kind')}] {e.get('code')}  {e.get('at')}  {e.get('desc')}")
c = collections.Counter(s.get("catalogo") for s in skips)
print("  catálogos no verificados:", dict(sorted(c.items())))
