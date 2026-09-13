using System;

namespace NppXsdViewer.Plugin.PluginInfrastructure
{
    /// <summary>
    /// A Scintilla UTF-8 bájtpufferben megkeresi az adott forrássorhoz tartozó
    /// XML elem teljes tartományát. A visszaadott pozíciók közvetlenül használhatók
    /// Scintilla dokumentumpozícióként.
    /// </summary>
    internal static class XmlSourceSpanFinder
    {
        internal readonly struct SourceSpan
        {
            internal SourceSpan(int start, int end)
            {
                Start = start;
                End = end;
            }

            internal int Start { get; }
            internal int End { get; }
            internal int Length => Math.Max(0, End - Start);
        }

        internal static bool TryFindElement(byte[] document, int approximatePosition, out SourceSpan span)
        {
            span = default;
            if (document == null || document.Length == 0)
                return false;

            var position = Math.Max(0, Math.Min(approximatePosition, document.Length - 1));
            if (!TryFindNextOpeningTag(document, position, out var start, out var tagEnd, out var tagName, out var selfClosing))
                return false;

            if (selfClosing)
            {
                span = new SourceSpan(start, tagEnd + 1);
                return true;
            }

            var depth = 1;
            var cursor = tagEnd + 1;
            while (cursor < document.Length)
            {
                var next = FindNextMarkup(document, cursor);
                if (next < 0)
                    break;

                if (StartsWith(document, next, "<!--"))
                {
                    cursor = SkipUntil(document, next + 4, "-->");
                    continue;
                }

                if (StartsWith(document, next, "<![CDATA["))
                {
                    cursor = SkipUntil(document, next + 9, "]]>");
                    continue;
                }

                if (StartsWith(document, next, "<?"))
                {
                    cursor = SkipUntil(document, next + 2, "?>");
                    continue;
                }

                if (StartsWith(document, next, "<!"))
                {
                    var declarationEnd = FindTagEnd(document, next + 2);
                    cursor = declarationEnd < 0 ? document.Length : declarationEnd + 1;
                    continue;
                }

                var closing = next + 1 < document.Length && document[next + 1] == (byte)'/';
                var nameStart = next + (closing ? 2 : 1);
                var nameEnd = ReadNameEnd(document, nameStart);
                if (nameEnd <= nameStart)
                {
                    cursor = next + 1;
                    continue;
                }

                var currentName = SliceAscii(document, nameStart, nameEnd - nameStart);
                var currentTagEnd = FindTagEnd(document, nameEnd);
                if (currentTagEnd < 0)
                    break;

                if (string.Equals(currentName, tagName, StringComparison.Ordinal))
                {
                    if (closing)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            span = new SourceSpan(start, currentTagEnd + 1);
                            return true;
                        }
                    }
                    else if (!IsSelfClosing(document, next, currentTagEnd))
                    {
                        depth++;
                    }
                }

                cursor = currentTagEnd + 1;
            }

            // Hibás vagy félkész XML esetén legalább a kezdő taget emeljük ki.
            span = new SourceSpan(start, tagEnd + 1);
            return true;
        }

        private static bool TryFindNextOpeningTag(
            byte[] document,
            int from,
            out int start,
            out int tagEnd,
            out string tagName,
            out bool selfClosing)
        {
            start = -1;
            tagEnd = -1;
            tagName = string.Empty;
            selfClosing = false;

            var cursor = from;
            while (cursor < document.Length)
            {
                var next = FindNextMarkup(document, cursor);
                if (next < 0)
                    return false;

                if (StartsWith(document, next, "<!--"))
                {
                    cursor = SkipUntil(document, next + 4, "-->");
                    continue;
                }

                if (StartsWith(document, next, "<![CDATA["))
                {
                    cursor = SkipUntil(document, next + 9, "]]>");
                    continue;
                }

                if (StartsWith(document, next, "<?"))
                {
                    cursor = SkipUntil(document, next + 2, "?>");
                    continue;
                }

                if (StartsWith(document, next, "<!") ||
                    (next + 1 < document.Length && document[next + 1] == (byte)'/'))
                {
                    var end = FindTagEnd(document, next + 1);
                    cursor = end < 0 ? document.Length : end + 1;
                    continue;
                }

                var nameStart = next + 1;
                var nameEnd = ReadNameEnd(document, nameStart);
                if (nameEnd <= nameStart)
                {
                    cursor = next + 1;
                    continue;
                }

                var endOfTag = FindTagEnd(document, nameEnd);
                if (endOfTag < 0)
                    return false;

                start = next;
                tagEnd = endOfTag;
                tagName = SliceAscii(document, nameStart, nameEnd - nameStart);
                selfClosing = IsSelfClosing(document, next, endOfTag);
                return true;
            }

            return false;
        }

        private static int FindNextMarkup(byte[] document, int from)
        {
            for (var i = Math.Max(0, from); i < document.Length; i++)
            {
                if (document[i] == (byte)'<')
                    return i;
            }
            return -1;
        }

        private static int FindTagEnd(byte[] document, int from)
        {
            byte quote = 0;
            for (var i = Math.Max(0, from); i < document.Length; i++)
            {
                var current = document[i];
                if (quote != 0)
                {
                    if (current == quote)
                        quote = 0;
                    continue;
                }

                if (current == (byte)'\'' || current == (byte)'\"')
                {
                    quote = current;
                    continue;
                }

                if (current == (byte)'>')
                    return i;
            }
            return -1;
        }

        private static int ReadNameEnd(byte[] document, int from)
        {
            var i = from;
            while (i < document.Length)
            {
                var c = document[i];
                if (IsNameByte(c))
                    i++;
                else
                    break;
            }
            return i;
        }

        private static bool IsNameByte(byte c)
            => (c >= (byte)'a' && c <= (byte)'z')
               || (c >= (byte)'A' && c <= (byte)'Z')
               || (c >= (byte)'0' && c <= (byte)'9')
               || c == (byte)'_'
               || c == (byte)':'
               || c == (byte)'-'
               || c == (byte)'.';

        private static bool IsSelfClosing(byte[] document, int tagStart, int tagEnd)
        {
            for (var i = tagEnd - 1; i > tagStart; i--)
            {
                var c = document[i];
                if (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r' || c == (byte)'\n')
                    continue;
                return c == (byte)'/';
            }
            return false;
        }

        private static bool StartsWith(byte[] document, int position, string value)
        {
            if (position < 0 || position + value.Length > document.Length)
                return false;
            for (var i = 0; i < value.Length; i++)
            {
                if (document[position + i] != (byte)value[i])
                    return false;
            }
            return true;
        }

        private static int SkipUntil(byte[] document, int from, string terminator)
        {
            for (var i = Math.Max(0, from); i <= document.Length - terminator.Length; i++)
            {
                if (StartsWith(document, i, terminator))
                    return i + terminator.Length;
            }
            return document.Length;
        }

        private static string SliceAscii(byte[] document, int start, int length)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
                chars[i] = (char)document[start + i];
            return new string(chars);
        }
    }
}
