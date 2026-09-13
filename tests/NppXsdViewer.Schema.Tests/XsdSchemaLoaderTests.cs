using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NppXsdViewer.Schema.Parsing;

namespace NppXsdViewer.Schema.Tests;

[TestClass]
public sealed class XsdSchemaLoaderTests
{
    [TestMethod]
    public void Load_ReadsGlobalElementComplexTypeAndEnumeration()
    {
        var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "invoice.xsd");
        var model = new XsdSchemaLoader().Load(path);

        Assert.AreEqual(1, model.GlobalElements.Count);
        Assert.AreEqual("Invoice", model.GlobalElements[0].Name);
        Assert.IsTrue(model.Types.Count >= 3);
    }
    [TestMethod]
    public void Load_RegistersAnonymousComplexTypeAndBaseType()
    {
        var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "anonymous-extension.xsd");
        var model = new XsdSchemaLoader().Load(path);

        var root = model.GlobalElements[0];
        Assert.AreEqual("AuthRequest", root.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.TypeName));
        Assert.IsTrue(root.TypeName.StartsWith("@anonymous:"));
        Assert.IsTrue(model.Types.ContainsKey(root.TypeName));

        var anonymous = model.Types[root.TypeName];
        Assert.AreEqual("BaseRequestType", anonymous.BaseTypeName.Substring(anonymous.BaseTypeName.LastIndexOf('}') + 1));
        Assert.IsTrue(anonymous.Elements.Count >= 1);
    }

    [TestMethod]
    public void Load_RegistersNestedChainAnonymousTypeAndSimpleFacets()
    {
        var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "chain-example.xsd");
        var model = new XsdSchemaLoader().Load(path);

        var root = model.GlobalElements[0];
        var rootType = model.Types[root.TypeName];
        var chain = rootType.Elements[0];
        var chainType = model.Types[chain.TypeName];
        var chainElem = chainType.Elements[0];

        Assert.IsTrue(chainElem.TypeName.StartsWith("@anonymous:"));
        Assert.IsTrue(model.Types.ContainsKey(chainElem.TypeName));
        var anonymous = model.Types[chainElem.TypeName];
        Assert.AreEqual("FieldGroup", anonymous.Elements[0].Name);

        var fieldGroup = model.Types[anonymous.Elements[0].TypeName];
        var code = fieldGroup.Elements[0];
        var codeType = model.Types[code.TypeName];
        Assert.AreEqual(1, codeType.Patterns.Count);
        Assert.AreEqual(2, codeType.EnumerationValues.Count);
        Assert.IsTrue(codeType.Facets.Count >= 2);
    }

    [TestMethod]
    public void Load_BuildsComponentBrowserAndReverseReferences()
    {
        var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "invoice.xsd");
        var model = new XsdSchemaLoader().Load(path);

        Assert.IsTrue(model.Components.Any(c => c.Kind == NppXsdViewer.Schema.Model.SchemaComponentKind.Element && c.Name == "Invoice"));
        Assert.IsTrue(model.Components.Any(c => c.Kind == NppXsdViewer.Schema.Model.SchemaComponentKind.ComplexType && c.Name == "InvoiceType"));
        Assert.IsTrue(model.Components.Any(c => c.Kind == NppXsdViewer.Schema.Model.SchemaComponentKind.SimpleType && c.Name == "CurrencyType"));

        var invoiceType = model.Types.Values.Single(t => t.Name == "InvoiceType");
        Assert.IsTrue(model.References.Any(r => r.TargetQualifiedName == invoiceType.QualifiedName && r.OwnerName == "Invoice"));

        var currencyType = model.Types.Values.Single(t => t.Name == "CurrencyType");
        Assert.IsTrue(model.References.Any(r => r.TargetQualifiedName == currencyType.QualifiedName && r.ReferenceKind == "attribute type"));
    }

}
