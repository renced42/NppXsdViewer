using System;
using System.IO;
using System.Net;
using System.Xml;

namespace NppXsdViewer.Schema.Parsing;

/// <summary>
/// Csak lokális file URI-kat engedélyező resolver.
/// </summary>
internal sealed class LocalOnlyXmlResolver : XmlResolver
{
    private readonly string rootDirectory;

    public LocalOnlyXmlResolver(string rootDirectory)
    {
        this.rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public override ICredentials? Credentials { set { } }

    public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
    {
        if (relativeUri == null)
            throw new XmlException("Hiányzó schemaLocation.");

        if (Uri.TryCreate(relativeUri, UriKind.Absolute, out var absolute))
            return EnsureLocal(absolute);

        if (baseUri != null && baseUri.IsAbsoluteUri)
            return EnsureLocal(new Uri(baseUri, relativeUri));

        return EnsureLocal(new Uri(Path.GetFullPath(Path.Combine(rootDirectory, relativeUri))));
    }

    public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        var local = EnsureLocal(absoluteUri);
        return File.Open(local.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    private static Uri EnsureLocal(Uri uri)
    {
        if (!uri.IsFile)
            throw new XmlException($"Külső XSD erőforrás tiltott: {uri.Scheme}");
        return uri;
    }
}
