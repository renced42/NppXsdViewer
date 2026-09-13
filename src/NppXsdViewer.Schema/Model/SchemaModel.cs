using System;
using System.Collections.Generic;

namespace NppXsdViewer.Schema.Model;

public sealed class SchemaModel
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetNamespace { get; set; } = string.Empty;
    public IReadOnlyList<SchemaElementModel> GlobalElements { get; set; } = Array.Empty<SchemaElementModel>();
    public IReadOnlyDictionary<string, SchemaTypeModel> Types { get; set; } = new Dictionary<string, SchemaTypeModel>();
    public IReadOnlyList<string> LoadedSchemas { get; set; } = Array.Empty<string>();
    public IReadOnlyList<SchemaComponentModel> Components { get; set; } = Array.Empty<SchemaComponentModel>();
    public IReadOnlyList<SchemaDependencyModel> Dependencies { get; set; } = Array.Empty<SchemaDependencyModel>();
    public IReadOnlyList<SchemaDiagnosticModel> Diagnostics { get; set; } = Array.Empty<SchemaDiagnosticModel>();
    public IReadOnlyList<SchemaReferenceModel> References { get; set; } = Array.Empty<SchemaReferenceModel>();
}

public sealed class SchemaElementModel
{
    public string Name { get; set; } = string.Empty;
    public string QualifiedName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public decimal MinOccurs { get; set; }
    public string MaxOccurs { get; set; } = "1";
    public string Documentation { get; set; } = string.Empty;
    public int? SourceLine { get; set; }
    public string SourceUri { get; set; } = string.Empty;
    public bool IsReference { get; set; }
    public bool IsAbstract { get; set; }
}

public sealed class SchemaTypeModel
{
    public string Name { get; set; } = string.Empty;
    public string QualifiedName { get; set; } = string.Empty;
    public string BaseTypeName { get; set; } = string.Empty;
    public SchemaCompositor Compositor { get; set; }
    public IReadOnlyList<SchemaElementModel> Elements { get; set; } = Array.Empty<SchemaElementModel>();
    public IReadOnlyList<SchemaAttributeModel> Attributes { get; set; } = Array.Empty<SchemaAttributeModel>();
    public IReadOnlyList<string> EnumerationValues { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Patterns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<SchemaFacetModel> Facets { get; set; } = Array.Empty<SchemaFacetModel>();
    public string Documentation { get; set; } = string.Empty;
    public int? SourceLine { get; set; }
    public string SourceUri { get; set; } = string.Empty;
    public bool IsSimple { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsAbstract { get; set; }
}

public sealed class SchemaAttributeModel
{
    public string Name { get; set; } = string.Empty;
    public string QualifiedName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string Documentation { get; set; } = string.Empty;
    public int? SourceLine { get; set; }
    public string SourceUri { get; set; } = string.Empty;
}

public sealed class SchemaFacetModel
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class SchemaComponentModel
{
    public SchemaComponentKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string QualifiedName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Documentation { get; set; } = string.Empty;
    public string SourceUri { get; set; } = string.Empty;
    public int? SourceLine { get; set; }
}

public sealed class SchemaDependencyModel
{
    public string SourceUri { get; set; } = string.Empty;
    public string TargetNamespace { get; set; } = string.Empty;
    public string SchemaLocation { get; set; } = string.Empty;
    public string ResolvedLocation { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public bool Resolved { get; set; }
}

public sealed class SchemaDiagnosticModel
{
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourceUri { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}

public sealed class SchemaReferenceModel
{
    public string TargetQualifiedName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerQualifiedName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string SourceUri { get; set; } = string.Empty;
    public int? SourceLine { get; set; }
    public string ReferenceKind { get; set; } = string.Empty;
}

public enum SchemaComponentKind
{
    Element,
    ComplexType,
    SimpleType,
    Group,
    AttributeGroup,
    Attribute,
    Import,
    Include
}

public enum SchemaCompositor
{
    None,
    Sequence,
    Choice,
    All,
    Simple
}
