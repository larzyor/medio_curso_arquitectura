import sys
from lxml import etree
xsd=etree.XMLSchema(etree.parse(sys.argv[1]))
d=etree.parse(sys.argv[2])
ok=xsd.validate(d)
print("XSD", "VÁLIDO" if ok else "INVÁLIDO")
for e in list(xsd.error_log)[:8]: print("  ",e.line,e.message[:200])
