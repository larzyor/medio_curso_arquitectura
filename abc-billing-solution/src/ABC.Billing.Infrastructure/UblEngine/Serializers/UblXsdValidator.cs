using System.Xml;
using System.Xml.Schema;
using ABC.Billing.Core.Domain;
using ABC.Billing.Core.Ports.Outbound;

namespace ABC.Billing.Infrastructure.UblEngine.Serializers;

/// <summary>Valida el XML contra los XSD oficiales de OASIS UBL 2.1 (carpeta Schemas junto al ensamblado).</summary>
public sealed class UblXsdValidator : IUblXsdValidator
{
    private readonly XmlSchemaSet _schemas = new();
    private readonly Dictionary<string, XmlSchema> _cargados = new(StringComparer.OrdinalIgnoreCase);

    public UblXsdValidator(string? carpetaSchemas = null)
    {
        var dir = carpetaSchemas ?? Path.Combine(AppContext.BaseDirectory, "Schemas");
        // Se parte del XSD de la factura y se siguen sus import/include. La carpeta "common" trae módulos
        // alternativos que NO deben cargarse juntos (declararían los mismos tipos dos veces).
        _schemas.Add(Cargar(Path.GetFullPath(Path.Combine(dir, "maindoc", "UBL-Invoice-2.1.xsd"))));
        _schemas.Compile();
    }

    /// <summary>Esquemas compilados (las pruebas los usan para comparar el modelo generado con el XSD).</summary>
    public XmlSchemaSet Esquemas => _schemas;

    private XmlSchema Cargar(string ruta)
    {
        if (_cargados.TryGetValue(ruta, out var existente)) return existente;

        // El esquema XMLDSig de UBL trae un DOCTYPE: se lee con DTD habilitado.
        var lectura = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() };
        using var r = XmlReader.Create(ruta, lectura);
        var schema = XmlSchema.Read(r, null)!;
        _cargados[ruta] = schema;

        foreach (var externo in schema.Includes.OfType<XmlSchemaExternal>())
        {
            if (string.IsNullOrEmpty(externo.SchemaLocation)) continue;
            var hijo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ruta)!, externo.SchemaLocation));
            externo.Schema = Cargar(hijo);
        }
        return schema;
    }

    public void Validar(byte[] xml)
    {
        var errores = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = _schemas, DtdProcessing = DtdProcessing.Prohibit };
        settings.ValidationEventHandler += (_, e) => errores.Add($"[{e.Severity}] línea {e.Exception?.LineNumber}: {e.Message}");

        using var ms = new MemoryStream(xml);
        using var reader = XmlReader.Create(ms, settings);
        while (reader.Read()) { }

        if (errores.Count > 0) throw new UblValidationException(errores);
    }
}
