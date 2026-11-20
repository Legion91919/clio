using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Agentic.Blocks.Core;
using Clio.Application;

namespace Clio.Infrastructure;

/// <summary>
/// A deliberately small reader for text-layer PDFs: classic object tables, page tree order, uncompressed or
/// FlateDecode content streams, literal and hex strings in simple (Latin-1) fonts. It does not do OCR, CID fonts or
/// compressed object streams; those are what a production adapter behind <see cref="IDocumentTextReader"/> adds
/// (see ADR-0003). Anything it cannot read becomes a clear "unreadable" error and the original is kept.
/// </summary>
internal static partial class SimplePdfTextReader
{
    private const string Kept = " The original is kept; ask for a text PDF or key the terms by hand.";

    public static Result<DocumentText> Read(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var objects = new Dictionary<int, string>();
        foreach (Match m in ObjectRegex().Matches(raw)) objects[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = m.Groups[2].Value;
        if (objects.Count == 0) return CompositeDocumentTextReader.Unreadable("The PDF is damaged or uses a structure this reader does not support." + Kept);

        var pageBodies = new List<string>();
        var root = RootRegex().Match(raw) is { Success: true } r ? Ref(r.Groups[1].Value) : objects.FirstOrDefault(o => o.Value.Contains("/Catalog", StringComparison.Ordinal)).Key;
        if (objects.TryGetValue(root, out var catalog) && PagesRefRegex().Match(catalog) is { Success: true } p)
            CollectPages(objects, Ref(p.Groups[1].Value), pageBodies, 0);
        if (pageBodies.Count == 0)
            return CompositeDocumentTextReader.Unreadable("The PDF has no readable page tree (it may use compressed object streams)." + Kept);

        var pages = new List<DocumentPage>();
        foreach (var page in pageBodies)
        {
            var lines = new List<string>();
            foreach (var contentRef in ContentRefs(page))
            {
                if (objects.TryGetValue(contentRef, out var body) && StreamData(body) is { } data) lines.AddRange(ContentText(data));
            }

            pages.Add(new DocumentPage(pages.Count + 1, lines));
        }

        return pages.Exists(pg => pg.Lines.Any(l => !string.IsNullOrWhiteSpace(l)))
            ? new DocumentText(pages)
            : CompositeDocumentTextReader.Unreadable("The PDF has no text layer (it may be a scanned image)." + Kept);
    }

    private static int Ref(string number) => int.Parse(number, CultureInfo.InvariantCulture);

    private static void CollectPages(Dictionary<int, string> objects, int id, List<string> pages, int depth)
    {
        if (depth > 32 || !objects.TryGetValue(id, out var node)) return;
        var kids = KidsRegex().Match(node);
        if (kids.Success)
        {
            foreach (Match k in RefRegex().Matches(kids.Groups[1].Value)) CollectPages(objects, Ref(k.Groups[1].Value), pages, depth + 1);
        }
        else if (PageTypeRegex().IsMatch(node))
        {
            pages.Add(node);
        }
    }

    private static IEnumerable<int> ContentRefs(string page)
    {
        var m = ContentsRegex().Match(page);
        if (!m.Success) return [];
        return RefRegex().Matches(m.Groups[1].Value).Select(x => Ref(x.Groups[1].Value));
    }

    private static string? StreamData(string body)
    {
        var start = body.IndexOf("stream", StringComparison.Ordinal);
        var end = body.LastIndexOf("endstream", StringComparison.Ordinal);
        if (start < 0 || end <= start) return null;
        var dict = body[..start];
        start += "stream".Length;
        if (start < body.Length && body[start] == '\r') start++;
        if (start < body.Length && body[start] == '\n') start++;
        var data = LengthRegex().Match(dict) is { Success: true } len && start + Ref(len.Groups[1].Value) <= end
            ? body.Substring(start, Ref(len.Groups[1].Value))
            : body[start..end].TrimEnd('\r', '\n');
        if (dict.Contains("/FlateDecode", StringComparison.Ordinal))
        {
            try
            {
                using var input = new MemoryStream(Encoding.Latin1.GetBytes(data));
                using var z = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                z.CopyTo(output);
                return Encoding.Latin1.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        return dict.Contains("/Filter", StringComparison.Ordinal) ? null : data;
    }

    /// <summary>Runs the text operators of a content stream and returns its non-empty lines.</summary>
    internal static List<string> ContentText(string s)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        var operands = new List<object>();
        double? lastY = null;
        void NewLine()
        {
            var text = line.ToString().Trim();
            if (text.Length > 0) lines.Add(text);
            line.Clear();
        }

        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '%') { while (i < s.Length && s[i] != '\n') i++; continue; }
            if (c == '(') { operands.Add(ReadLiteral(s, ref i)); continue; }
            if (c == '<' && i + 1 < s.Length && s[i + 1] != '<') { operands.Add(ReadHex(s, ref i)); continue; }
            if (c == '[') { operands.Add(ReadArray(s, ref i)); continue; }
            if (c is '<' or '>' or ']' or '{' or '}') { i++; continue; }
            if (c == '/') { i++; while (i < s.Length && !IsDelimiter(s[i])) i++; operands.Add("/name"); continue; }
            if (char.IsDigit(c) || c is '-' or '+' or '.')
            {
                var start = i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                operands.Add(double.TryParse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d);
                continue;
            }

            var opStart = i;
            while (i < s.Length && !IsDelimiter(s[i])) i++;
            if (i == opStart) { i++; continue; }
            var op = s[opStart..i];
            switch (op)
            {
                case "Tj": if (operands.LastOrDefault() is TextOperand t) line.Append(t.Text); break;
                case "TJ":
                    if (operands.LastOrDefault() is List<object> arr)
                    {
                        foreach (var item in arr)
                        {
                            if (item is TextOperand part) line.Append(part.Text);
                            else if (item is double gap && gap < -200) line.Append(' ');
                        }
                    }

                    break;
                case "'": NewLine(); if (operands.LastOrDefault() is TextOperand q) line.Append(q.Text); break;
                case "\"": NewLine(); if (operands.LastOrDefault() is TextOperand dq) line.Append(dq.Text); break;
                case "T*": NewLine(); break;
                case "Td" or "TD": if (operands.Count >= 2 && operands[^1] is double ty && ty != 0) NewLine(); break;
                case "Tm":
                    if (operands.Count >= 6 && operands[^1] is double y)
                    {
                        if (lastY is { } prev && Math.Abs(prev - y) > 0.01) NewLine();
                        lastY = y;
                    }

                    break;
                case "ET": NewLine(); break;
                case "ID":
                    var ei = s.IndexOf("EI", i, StringComparison.Ordinal);
                    i = ei < 0 ? s.Length : ei + 2;
                    break;
            }

            operands.Clear();
        }

        NewLine();
        return lines;
    }

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || c is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';

    private static TextOperand ReadLiteral(string s, ref int i)
    {
        var sb = new StringBuilder();
        var depth = 0;
        i++;
        while (i < s.Length)
        {
            var c = s[i++];
            if (c == '\\' && i < s.Length)
            {
                var e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '\r': if (i < s.Length && s[i] == '\n') i++; break;
                    case '\n': break;
                    case >= '0' and <= '7':
                        var oct = e - '0';
                        for (var k = 0; k < 2 && i < s.Length && s[i] is >= '0' and <= '7'; k++) oct = (oct * 8) + (s[i++] - '0');
                        sb.Append((char)(oct & 0xFF));
                        break;
                    default: sb.Append(e); break;
                }
            }
            else if (c == '(') { depth++; sb.Append(c); }
            else if (c == ')') { if (depth-- == 0) break; sb.Append(c); }
            else sb.Append(c);
        }

        return new TextOperand(sb.ToString());
    }

    private static TextOperand ReadHex(string s, ref int i)
    {
        var end = s.IndexOf('>', i);
        if (end < 0) end = s.Length;
        var hex = new string(s[(i + 1)..end].Where(Uri.IsHexDigit).ToArray());
        if (hex.Length % 2 == 1) hex += "0";
        i = Math.Min(end + 1, s.Length);
        return new TextOperand(Encoding.Latin1.GetString(Convert.FromHexString(hex)));
    }

    private static List<object> ReadArray(string s, ref int i)
    {
        var items = new List<object>();
        i++;
        while (i < s.Length && s[i] != ']')
        {
            var c = s[i];
            if (c == '(') items.Add(ReadLiteral(s, ref i));
            else if (c == '<') items.Add(ReadHex(s, ref i));
            else if (char.IsDigit(c) || c is '-' or '+' or '.')
            {
                var start = i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                items.Add(double.TryParse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d);
            }
            else i++;
        }

        i++;
        return items;
    }

    private sealed record TextOperand(string Text);

    [GeneratedRegex(@"(\d+)\s+\d+\s+obj\b(.*?)\bendobj", RegexOptions.Singleline)]
    private static partial Regex ObjectRegex();

    [GeneratedRegex(@"/Root\s+(\d+)\s+\d+\s+R")]
    private static partial Regex RootRegex();

    [GeneratedRegex(@"/Pages\s+(\d+)\s+\d+\s+R")]
    private static partial Regex PagesRefRegex();

    [GeneratedRegex(@"/Kids\s*\[([^\]]*)\]")]
    private static partial Regex KidsRegex();

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PageTypeRegex();

    [GeneratedRegex(@"/Contents\s*(\[[^\]]*\]|\d+\s+\d+\s+R)")]
    private static partial Regex ContentsRegex();

    [GeneratedRegex(@"(\d+)\s+\d+\s+R")]
    private static partial Regex RefRegex();

    [GeneratedRegex(@"/Length\s+(\d+)(?!\s+\d+\s+R)")]
    private static partial Regex LengthRegex();
}
