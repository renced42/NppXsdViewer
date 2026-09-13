using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using NppXsdViewer.Schema.Model;

namespace NppXsdViewer.Schema.Parsing;

/// <summary>
/// Loads an XSD and its local or HTTP/HTTPS include/import dependencies and builds
/// a read-only model used by the Notepad++ schema viewer.
/// </summary>
public sealed class XsdSchemaLoader
{
    public SchemaModel Load(string schemaPath)
    {
        if (string.IsNullOrWhiteSpace(schemaPath))
            throw new ArgumentException("The XSD path is required.", nameof(schemaPath));

        var fullPath = Path.GetFullPath(schemaPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The XSD file was not found.", fullPath);

        var resolver = new XsdResourceResolver(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        var schemaSet = new XmlSchemaSet { XmlResolver = resolver };
        var diagnostics = new List<SchemaDiagnosticModel>();

        schemaSet.ValidationEventHandler += (_, args) =>
        {
            var exception = args.Exception;
            diagnostics.Add(new SchemaDiagnosticModel
            {
                Severity = args.Severity.ToString(),
                Message = args.Message,
                SourceUri = exception?.SourceUri ?? string.Empty,
                Line = exception?.LineNumber ?? 0,
                Column = exception?.LinePosition ?? 0
            });
        };

        try
        {
            using (var reader = CreateReader(fullPath, resolver))
                schemaSet.Add(null, reader);

            schemaSet.Compile();
        }
        catch (Exception ex)
        {
            throw new XmlSchemaException(BuildLoadError(ex, diagnostics, resolver), ex);
        }

        // Keep the schema browsable even when the compiler reports recoverable XSD errors.
        // The diagnostics are exposed in SchemaModel.Diagnostics and shown in the Problems tab.
        return BuildModel(fullPath, schemaSet, diagnostics);
    }

    private static string BuildLoadError(
        Exception? exception,
        IReadOnlyCollection<SchemaDiagnosticModel> schemaDiagnostics,
        XsdResourceResolver resolver)
    {
        var parts = new List<string>();
        if (exception != null)
            parts.Add("Exception: " + exception.GetType().FullName + ": " + exception.Message);

        if (schemaDiagnostics.Count > 0)
        {
            parts.Add("XSD diagnostics:");
            parts.AddRange(schemaDiagnostics.Select(d =>
                $"{d.Severity}: {d.Message} [{d.SourceUri}:{d.Line}:{d.Column}]"));
        }

        parts.Add("Resolver diagnostics:");
        parts.Add(resolver.FormatDiagnostics());
        parts.Add("Detailed resolver log: " + Path.Combine(Path.GetTempPath(), "NppXsdViewer-resolver.log"));
        return string.Join(Environment.NewLine, parts);
    }

    private static XmlReader CreateReader(string path, XmlResolver resolver)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = resolver,
            IgnoreComments = false,
            IgnoreWhitespace = false
        };
        return XmlReader.Create(path, settings);
    }

    private static SchemaModel BuildModel(
        string sourcePath,
        XmlSchemaSet set,
        IReadOnlyList<SchemaDiagnosticModel> diagnostics)
    {
        var types = new Dictionary<string, SchemaTypeModel>(StringComparer.Ordinal);
        var buildingTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in set.GlobalTypes.Values.OfType<XmlSchemaType>().Where(t => !t.QualifiedName.IsEmpty))
            RegisterType(type, QName(type.QualifiedName), types, buildingTypes, "global:" + QName(type.QualifiedName));

        var globalElements = set.GlobalElements.Values
            .OfType<XmlSchemaElement>()
            .OrderBy(e => e.QualifiedName.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => ToElementModel(e, types, buildingTypes, "root:" + QName(e.QualifiedName)))
            .ToArray();

        var schemas = set.Schemas().Cast<XmlSchema>().ToArray();
        var loadedSchemas = schemas
            .Select(s => s.SourceUri ?? string.Empty)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var primary = schemas.FirstOrDefault(s =>
            string.Equals(UriToPath(s.SourceUri), sourcePath, StringComparison.OrdinalIgnoreCase));

        var dependencies = BuildDependencies(schemas);
        var components = BuildComponents(schemas, globalElements, types, dependencies);
        var references = BuildReferences(globalElements, types);

        return new SchemaModel
        {
            SourcePath = sourcePath,
            TargetNamespace = primary?.TargetNamespace ?? string.Empty,
            GlobalElements = globalElements,
            Types = types,
            LoadedSchemas = loadedSchemas,
            Components = components,
            Dependencies = dependencies,
            Diagnostics = diagnostics.ToArray(),
            References = references
        };
    }

    private static SchemaElementModel ToElementModel(
        XmlSchemaElement element,
        IDictionary<string, SchemaTypeModel> types,
        ISet<string> buildingTypes,
        string path)
    {
        var maxOccurs = element.MaxOccursString == "unbounded"
            ? "unbounded"
            : element.MaxOccurs.ToString(CultureInfo.InvariantCulture);

        return new SchemaElementModel
        {
            Name = ElementName(element),
            QualifiedName = QName(element.QualifiedName),
            TypeName = ResolveTypeName(element, types, buildingTypes, path),
            MinOccurs = element.MinOccurs,
            MaxOccurs = maxOccurs,
            Documentation = Documentation(element.Annotation),
            SourceLine = element.LineNumber > 0 ? element.LineNumber : null,
            SourceUri = element.SourceUri ?? string.Empty,
            IsReference = !element.RefName.IsEmpty,
            IsAbstract = element.IsAbstract
        };
    }

    private static string ResolveTypeName(
        XmlSchemaElement element,
        IDictionary<string, SchemaTypeModel> types,
        ISet<string> buildingTypes,
        string path)
    {
        if (!element.SchemaTypeName.IsEmpty)
            return QName(element.SchemaTypeName);

        if (element.ElementSchemaType == null)
            return string.Empty;

        if (!element.ElementSchemaType.QualifiedName.IsEmpty)
            return QName(element.ElementSchemaType.QualifiedName);

        var syntheticName = "@anonymous:" + path;
        RegisterType(element.ElementSchemaType, syntheticName, types, buildingTypes, path);
        return syntheticName;
    }

    private static void RegisterType(
        XmlSchemaType type,
        string key,
        IDictionary<string, SchemaTypeModel> types,
        ISet<string> buildingTypes,
        string path)
    {
        if (types.ContainsKey(key) || !buildingTypes.Add(key))
            return;

        types[key] = new SchemaTypeModel
        {
            Name = DisplayTypeName(type, key),
            QualifiedName = key,
            BaseTypeName = QName(type.BaseXmlSchemaType?.QualifiedName ?? XmlQualifiedName.Empty),
            Compositor = type is XmlSchemaSimpleType ? SchemaCompositor.Simple : SchemaCompositor.None,
            Documentation = Documentation(type.Annotation),
            SourceLine = type.LineNumber > 0 ? type.LineNumber : null,
            SourceUri = type.SourceUri ?? string.Empty,
            IsSimple = type is XmlSchemaSimpleType,
            IsAnonymous = key.StartsWith("@anonymous:", StringComparison.Ordinal),
            IsAbstract = type is XmlSchemaComplexType complexType && complexType.IsAbstract
        };

        if (type is XmlSchemaSimpleType simple)
        {
            types[key] = ToSimpleTypeModel(simple, key);
            buildingTypes.Remove(key);
            return;
        }

        var complex = (XmlSchemaComplexType)type;
        var particle = complex.ContentTypeParticle;
        var compositor = particle switch
        {
            XmlSchemaSequence => SchemaCompositor.Sequence,
            XmlSchemaChoice => SchemaCompositor.Choice,
            XmlSchemaAll => SchemaCompositor.All,
            _ => SchemaCompositor.None
        };

        var childIndex = 0;
        var elements = FlattenElements(particle)
            .Select(child => ToElementModel(child, types, buildingTypes,
                path + "/" + ElementName(child) + "#" + childIndex++))
            .ToArray();

        var attributes = complex.AttributeUses.Values
            .OfType<XmlSchemaAttribute>()
            .OrderBy(a => a.QualifiedName.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new SchemaAttributeModel
            {
                Name = a.QualifiedName.IsEmpty ? a.Name ?? a.RefName.Name : a.QualifiedName.Name,
                QualifiedName = QName(a.QualifiedName),
                TypeName = QName(a.SchemaTypeName),
                Required = a.Use == XmlSchemaUse.Required,
                Documentation = Documentation(a.Annotation),
                SourceLine = a.LineNumber > 0 ? a.LineNumber : null,
                SourceUri = a.SourceUri ?? string.Empty
            })
            .ToArray();

        types[key] = new SchemaTypeModel
        {
            Name = DisplayTypeName(type, key),
            QualifiedName = key,
            BaseTypeName = QName(type.BaseXmlSchemaType?.QualifiedName ?? XmlQualifiedName.Empty),
            Compositor = compositor,
            Elements = elements,
            Attributes = attributes,
            Documentation = Documentation(type.Annotation),
            SourceLine = type.LineNumber > 0 ? type.LineNumber : null,
            SourceUri = type.SourceUri ?? string.Empty,
            IsSimple = false,
            IsAnonymous = key.StartsWith("@anonymous:", StringComparison.Ordinal),
            IsAbstract = complex.IsAbstract
        };

        buildingTypes.Remove(key);
    }

    private static SchemaTypeModel ToSimpleTypeModel(XmlSchemaSimpleType simple, string key)
    {
        var enumValues = Array.Empty<string>();
        var patterns = Array.Empty<string>();
        var facets = Array.Empty<SchemaFacetModel>();

        if (simple.Content is XmlSchemaSimpleTypeRestriction restriction)
        {
            enumValues = restriction.Facets.OfType<XmlSchemaEnumerationFacet>()
                .Select(f => f.Value ?? string.Empty).Where(v => v.Length > 0).ToArray();
            patterns = restriction.Facets.OfType<XmlSchemaPatternFacet>()
                .Select(f => f.Value ?? string.Empty).Where(v => v.Length > 0).ToArray();
            facets = restriction.Facets.OfType<XmlSchemaFacet>()
                .Where(f => !(f is XmlSchemaEnumerationFacet) && !(f is XmlSchemaPatternFacet))
                .Select(f => new SchemaFacetModel { Name = FacetName(f), Value = f.Value ?? string.Empty })
                .ToArray();
        }

        return new SchemaTypeModel
        {
            Name = DisplayTypeName(simple, key),
            QualifiedName = key,
            BaseTypeName = QName(simple.BaseXmlSchemaType?.QualifiedName ?? XmlQualifiedName.Empty),
            Compositor = SchemaCompositor.Simple,
            EnumerationValues = enumValues,
            Patterns = patterns,
            Facets = facets,
            Documentation = Documentation(simple.Annotation),
            SourceLine = simple.LineNumber > 0 ? simple.LineNumber : null,
            SourceUri = simple.SourceUri ?? string.Empty,
            IsSimple = true,
            IsAnonymous = key.StartsWith("@anonymous:", StringComparison.Ordinal)
        };
    }

    private static IReadOnlyList<SchemaDependencyModel> BuildDependencies(IEnumerable<XmlSchema> schemas)
    {
        var result = new List<SchemaDependencyModel>();
        foreach (var schema in schemas)
        {
            var sourceUri = schema.SourceUri ?? string.Empty;
            foreach (var external in schema.Includes.OfType<XmlSchemaExternal>())
            {
                var relation = external is XmlSchemaImport ? "import" : external is XmlSchemaInclude ? "include" : "redefine";
                var targetNamespace = (external as XmlSchemaImport)?.Namespace ?? string.Empty;
                var location = external.SchemaLocation ?? string.Empty;
                var resolved = ResolveLocation(sourceUri, location);
                result.Add(new SchemaDependencyModel
                {
                    SourceUri = sourceUri,
                    TargetNamespace = targetNamespace,
                    SchemaLocation = location,
                    ResolvedLocation = resolved,
                    Relation = relation,
                    Resolved = !string.IsNullOrWhiteSpace(resolved)
                });
            }
        }

        return result
            .OrderBy(d => d.SourceUri, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Relation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.SchemaLocation, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveLocation(string sourceUri, string schemaLocation)
    {
        if (string.IsNullOrWhiteSpace(schemaLocation))
            return string.Empty;

        if (Uri.TryCreate(schemaLocation, UriKind.Absolute, out var absolute))
            return absolute.AbsoluteUri;

        if (Uri.TryCreate(sourceUri, UriKind.Absolute, out var source))
        {
            try { return new Uri(source, schemaLocation).AbsoluteUri; }
            catch (UriFormatException) { return schemaLocation; }
        }

        try
        {
            var baseDir = Path.GetDirectoryName(sourceUri) ?? string.Empty;
            return Path.GetFullPath(Path.Combine(baseDir, schemaLocation));
        }
        catch
        {
            return schemaLocation;
        }
    }

    private static IReadOnlyList<SchemaComponentModel> BuildComponents(
        IEnumerable<XmlSchema> schemas,
        IReadOnlyList<SchemaElementModel> globalElements,
        IReadOnlyDictionary<string, SchemaTypeModel> types,
        IReadOnlyList<SchemaDependencyModel> dependencies)
    {
        var components = new List<SchemaComponentModel>();

        foreach (var element in globalElements)
        {
            components.Add(new SchemaComponentModel
            {
                Kind = SchemaComponentKind.Element,
                Name = element.Name,
                QualifiedName = element.QualifiedName,
                TypeName = element.TypeName,
                Documentation = element.Documentation,
                SourceUri = element.SourceUri,
                SourceLine = element.SourceLine
            });
        }

        foreach (var type in types.Values.Where(t => !t.IsAnonymous))
        {
            components.Add(new SchemaComponentModel
            {
                Kind = type.IsSimple ? SchemaComponentKind.SimpleType : SchemaComponentKind.ComplexType,
                Name = type.Name,
                QualifiedName = type.QualifiedName,
                TypeName = type.QualifiedName,
                Documentation = type.Documentation,
                SourceUri = type.SourceUri,
                SourceLine = type.SourceLine
            });
        }

        foreach (var schema in schemas)
        {
            foreach (XmlSchemaGroup group in schema.Groups.Values)
                components.Add(ComponentFromObject(SchemaComponentKind.Group, group.Name ?? string.Empty, group.QualifiedName, group));
            foreach (XmlSchemaAttributeGroup group in schema.AttributeGroups.Values)
                components.Add(ComponentFromObject(SchemaComponentKind.AttributeGroup, group.Name ?? string.Empty, group.QualifiedName, group));
            foreach (XmlSchemaAttribute attribute in schema.Attributes.Values)
            {
                var component = ComponentFromObject(SchemaComponentKind.Attribute,
                    attribute.QualifiedName.IsEmpty ? attribute.Name ?? string.Empty : attribute.QualifiedName.Name,
                    attribute.QualifiedName,
                    attribute);
                component.TypeName = QName(attribute.SchemaTypeName);
                components.Add(component);
            }
        }

        foreach (var dependency in dependencies)
        {
            components.Add(new SchemaComponentModel
            {
                Kind = string.Equals(dependency.Relation, "include", StringComparison.OrdinalIgnoreCase)
                    ? SchemaComponentKind.Include
                    : SchemaComponentKind.Import,
                Name = dependency.SchemaLocation,
                QualifiedName = dependency.ResolvedLocation,
                TypeName = dependency.TargetNamespace,
                SourceUri = dependency.SourceUri
            });
        }

        return components
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SchemaComponentModel ComponentFromObject(
        SchemaComponentKind kind,
        string name,
        XmlQualifiedName qualifiedName,
        XmlSchemaAnnotated annotated)
        => new SchemaComponentModel
        {
            Kind = kind,
            Name = name,
            QualifiedName = QName(qualifiedName),
            Documentation = Documentation(annotated.Annotation),
            SourceUri = annotated.SourceUri ?? string.Empty,
            SourceLine = annotated.LineNumber > 0 ? annotated.LineNumber : null
        };

    private static IReadOnlyList<SchemaReferenceModel> BuildReferences(
        IReadOnlyList<SchemaElementModel> globalElements,
        IReadOnlyDictionary<string, SchemaTypeModel> types)
    {
        var references = new List<SchemaReferenceModel>();

        foreach (var element in globalElements)
            AddReference(references, element.TypeName, element.Name, element.QualifiedName, element.Name,
                element.SourceUri, element.SourceLine, "element type");

        foreach (var type in types.Values)
        {
            if (!string.IsNullOrWhiteSpace(type.BaseTypeName))
                AddReference(references, type.BaseTypeName, type.Name, type.QualifiedName, type.Name,
                    type.SourceUri, type.SourceLine, "base type");

            foreach (var element in type.Elements)
                AddReference(references, element.TypeName, type.Name, type.QualifiedName,
                    type.Name + "/" + element.Name, element.SourceUri, element.SourceLine, "child element");

            foreach (var attribute in type.Attributes)
                AddReference(references, attribute.TypeName, type.Name, type.QualifiedName,
                    type.Name + "/@" + attribute.Name, attribute.SourceUri, attribute.SourceLine, "attribute type");
        }

        return references
            .Where(r => !string.IsNullOrWhiteSpace(r.TargetQualifiedName))
            .OrderBy(r => r.TargetQualifiedName, StringComparer.Ordinal)
            .ThenBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddReference(
        ICollection<SchemaReferenceModel> references,
        string target,
        string ownerName,
        string ownerQualifiedName,
        string path,
        string sourceUri,
        int? sourceLine,
        string referenceKind)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;

        references.Add(new SchemaReferenceModel
        {
            TargetQualifiedName = target,
            OwnerName = ownerName,
            OwnerQualifiedName = ownerQualifiedName,
            Path = path,
            SourceUri = sourceUri,
            SourceLine = sourceLine,
            ReferenceKind = referenceKind
        });
    }

    private static string FacetName(XmlSchemaFacet facet)
    {
        var name = facet.GetType().Name;
        const string prefix = "XmlSchema";
        const string suffix = "Facet";
        if (name.StartsWith(prefix, StringComparison.Ordinal))
            name = name.Substring(prefix.Length);
        if (name.EndsWith(suffix, StringComparison.Ordinal))
            name = name.Substring(0, name.Length - suffix.Length);
        return name;
    }

    private static string DisplayTypeName(XmlSchemaType type, string key)
    {
        if (!type.QualifiedName.IsEmpty)
            return type.QualifiedName.Name;
        return key.StartsWith("@anonymous:", StringComparison.Ordinal) ? "(anonymous complexType)" : key;
    }

    private static string ElementName(XmlSchemaElement element)
        => element.QualifiedName.IsEmpty ? element.Name ?? element.RefName.Name : element.QualifiedName.Name;

    private static IEnumerable<XmlSchemaElement> FlattenElements(XmlSchemaParticle particle)
    {
        if (particle is XmlSchemaElement element)
        {
            yield return element;
            yield break;
        }

        if (!(particle is XmlSchemaGroupBase group))
            yield break;

        foreach (var item in group.Items)
        {
            if (item is XmlSchemaElement child)
                yield return child;
            else if (item is XmlSchemaGroupBase nested)
            {
                foreach (var nestedElement in FlattenElements(nested))
                    yield return nestedElement;
            }
        }
    }

    private static string Documentation(XmlSchemaAnnotation? annotation)
    {
        if (annotation == null)
            return string.Empty;

        return string.Join(" ", annotation.Items
            .OfType<XmlSchemaDocumentation>()
            .SelectMany(d => d.Markup ?? Array.Empty<XmlNode>())
            .Select(n => n.InnerText.Trim())
            .Where(s => s.Length > 0));
    }

    private static string QName(XmlQualifiedName name)
        => name.IsEmpty ? string.Empty : $"{{{name.Namespace}}}{name.Name}";

    private static string UriToPath(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return string.Empty;
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile
            ? parsed.LocalPath
            : uri;
    }
}
