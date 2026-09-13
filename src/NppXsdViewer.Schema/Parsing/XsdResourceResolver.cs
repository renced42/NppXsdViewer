using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Xml;

namespace NppXsdViewer.Schema.Parsing;

/// <summary>
/// XSD erőforrás-feloldó lokális fájlokhoz és HTTP/HTTPS hivatkozásokhoz.
/// A hálózati letöltés a Windows alapértelmezett proxybeállításait és hitelesítését használja.
/// </summary>
internal sealed class XsdResourceResolver : XmlResolver
{
    private const int NetworkTimeoutMilliseconds = 30000;
    private const int MaximumRemoteSchemaBytes = 10 * 1024 * 1024;

    private readonly string rootDirectory;
    private readonly List<string> diagnostics = new List<string>();

    public XsdResourceResolver(string rootDirectory)
    {
        this.rootDirectory = Path.GetFullPath(rootDirectory);

        // A GitHub és a legtöbb modern HTTPS végpont legalább TLS 1.2-t igényel.
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    }

    public IReadOnlyList<string> Diagnostics => diagnostics;

    public override ICredentials? Credentials { set { } }

    public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
    {
        if (string.IsNullOrWhiteSpace(relativeUri))
            throw new XmlException("Missing schemaLocation.");

        Uri resolved;
        if (Uri.TryCreate(relativeUri, UriKind.Absolute, out var absolute))
        {
            resolved = absolute;
        }
        else if (baseUri != null && baseUri.IsAbsoluteUri)
        {
            resolved = new Uri(baseUri, relativeUri);
        }
        else
        {
            resolved = new Uri(Path.GetFullPath(Path.Combine(rootDirectory, relativeUri)));
        }

        EnsureAllowedScheme(resolved);

        if (baseUri != null && IsHttp(baseUri) && resolved.IsFile)
            throw new XmlException("Resolving a local file: resource from a remote XSD is not allowed.");

        Log("RESOLVE", (baseUri?.ToString() ?? "<no base URI>") + " + " + relativeUri + " -> " + resolved);
        return resolved;
    }

    public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        EnsureAllowedScheme(absoluteUri);

        if (absoluteUri.IsFile)
        {
            Log("FILE", absoluteUri.LocalPath);
            return File.Open(absoluteUri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        return DownloadRemoteSchema(absoluteUri);
    }

    public string FormatDiagnostics()
    {
        if (diagnostics.Count == 0)
            return "No resolver diagnostics available.";

        return string.Join(Environment.NewLine, diagnostics);
    }

    private Stream DownloadRemoteSchema(Uri uri)
    {
        Log("HTTP", "GET " + uri);

        try
        {
            var request = WebRequest.CreateHttp(uri);
            request.Method = "GET";
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = NetworkTimeoutMilliseconds;
            request.ReadWriteTimeout = NetworkTimeoutMilliseconds;
            request.UserAgent = "NppXsdViewer/0.2.1";
            request.Accept = "application/xml,text/xml,application/xsd+xml,text/plain,*/*";
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            request.KeepAlive = false;

            // Vállalati Windows környezetben a böngésző által használt rendszerproxy gyakori.
            request.Proxy = WebRequest.DefaultWebProxy;
            if (request.Proxy != null)
                request.Proxy.Credentials = CredentialCache.DefaultCredentials;

            using var response = (HttpWebResponse)request.GetResponse();
            var finalUri = response.ResponseUri ?? uri;
            if (!IsHttp(finalUri))
                throw new XmlException($"Disallowed redirect while downloading XSD: {uri} -> {finalUri}");

            Log("HTTP", ((int)response.StatusCode) + " " + response.StatusDescription + " | " + finalUri);

            if (response.ContentLength > MaximumRemoteSchemaBytes)
                throw new XmlException($"The remote XSD is too large: {response.ContentLength} byte. Maximum: {MaximumRemoteSchemaBytes} byte.");

            using var input = response.GetResponseStream()
                ?? throw new XmlException($"The remote XSD cannot be read: {uri}");

            var output = new MemoryStream();
            var buffer = new byte[81920];
            var total = 0;
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                total += read;
                if (total > MaximumRemoteSchemaBytes)
                    throw new XmlException($"The remote XSD is too large. Maximum: {MaximumRemoteSchemaBytes} byte.");

                output.Write(buffer, 0, read);
            }

            output.Position = 0;
            Log("HTTP", "DOWNLOADED " + total + " byte | " + finalUri);
            return output;
        }
        catch (WebException ex)
        {
            var detail = BuildWebExceptionDetail(uri, ex);
            Log("HTTP-HIBA", detail);
            throw new XmlException(detail, ex);
        }
        catch (Exception ex)
        {
            var detail = "XSD download error: " + uri + " | " + ex.GetType().FullName + ": " + ex.Message;
            Log("HTTP-HIBA", detail);
            throw new XmlException(detail, ex);
        }
    }

    private static string BuildWebExceptionDetail(Uri uri, WebException ex)
    {
        var builder = new StringBuilder();
        builder.Append("XSD HTTP error: ").Append(uri)
            .Append(" | WebExceptionStatus=").Append(ex.Status)
            .Append(" | ").Append(ex.Message);

        if (ex.Response is HttpWebResponse response)
        {
            builder.Append(" | HTTP=").Append((int)response.StatusCode)
                .Append(' ').Append(response.StatusDescription)
                .Append(" | FinalUri=").Append(response.ResponseUri);
        }

        if (ex.InnerException != null)
            builder.Append(" | Inner=").Append(ex.InnerException.GetType().FullName)
                .Append(": ").Append(ex.InnerException.Message);

        return builder.ToString();
    }

    private void Log(string category, string message)
    {
        var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + category + " | " + message;
        diagnostics.Add(line);

        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "NppXsdViewer-resolver.log"),
                line + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // A diagnosztikai napló hibája nem akadályozhatja a séma betöltését.
        }
    }

    private static void EnsureAllowedScheme(Uri uri)
    {
        if (uri.IsFile || IsHttp(uri))
            return;

        throw new XmlException($"Unsupported XSD resource scheme: {uri.Scheme}");
    }

    private static bool IsHttp(Uri uri)
        => uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
           || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
