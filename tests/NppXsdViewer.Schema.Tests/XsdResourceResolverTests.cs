using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NppXsdViewer.Schema.Parsing;

namespace NppXsdViewer.Schema.Tests;

[TestClass]
public sealed class XsdResourceResolverTests
{
    [TestMethod]
    public void ResolveUri_AllowsHttpsAbsoluteSchemaLocation()
    {
        var resolver = new XsdResourceResolver(Path.GetTempPath());
        var uri = resolver.ResolveUri(null, "https://raw.githubusercontent.com/example/repo/main/schema.xsd");

        Assert.AreEqual("https", uri.Scheme);
        Assert.AreEqual("raw.githubusercontent.com", uri.Host);
    }

    [TestMethod]
    public void ResolveUri_ResolvesRelativeSchemaAgainstRemoteBase()
    {
        var resolver = new XsdResourceResolver(Path.GetTempPath());
        var baseUri = new Uri("https://example.test/xsd/common/service.xsd");
        var uri = resolver.ResolveUri(baseUri, "../string.xsd");

        Assert.AreEqual("https://example.test/xsd/string.xsd", uri.AbsoluteUri);
    }

    [TestMethod]
    public void ResolveUri_RejectsRemoteSchemaSwitchingToLocalFile()
    {
        var resolver = new XsdResourceResolver(Path.GetTempPath());
        var baseUri = new Uri("https://example.test/xsd/service.xsd");

        Assert.ThrowsExactly<System.Xml.XmlException>(() =>
            resolver.ResolveUri(baseUri, "file:///C:/Windows/win.ini"));
    }
}
