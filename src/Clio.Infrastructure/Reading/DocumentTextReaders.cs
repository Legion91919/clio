using System.Text;
using Agentic.Blocks.Core;
using Clio.Application;

namespace Clio.Infrastructure;

/// <summary>Picks the reader by content: text-layer PDF (by the <c>%PDF-</c> header) or plain text/markdown.</summary>
internal sealed class CompositeDocumentTextReader : IDocumentTextReader
{
    internal const string UnreadableCode = "document.unreadable";

    public Result<DocumentText> Read(byte[] content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.AsSpan().StartsWith("%PDF-"u8)) return SimplePdfTextReader.Read(content);
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Unreadable("The file is named .pdf but is not a valid PDF. The original is kept.");
        return PlainTextReader.Read(content);
    }

    internal static Error Unreadable(string message) => Error.Validation(UnreadableCode, message);
}

/// <summary>Reads UTF-8 (or Latin-1) text. Pages split on form feed or a line reading <c>--- page break ---</c>.</summary>
internal static class PlainTextReader
{
    private const string PageBreak = "--- page break ---";

    public static Result<DocumentText> Read(byte[] content)
    {
        var controls = content.Count(b => b < 0x20 && b is not (0x09 or 0x0A or 0x0D or 0x0C));
        if (content.Contains((byte)0) || controls > content.Length / 20)
            return CompositeDocumentTextReader.Unreadable("This is not a text file or a text-layer PDF (it looks like an image or binary file). The original is kept; ask for a text PDF or key the terms by hand.");

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            text = Encoding.Latin1.GetString(content);
        }

        text = text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(text)) return CompositeDocumentTextReader.Unreadable("The file has no text. The original is kept.");

        var pages = new List<DocumentPage>();
        var current = new List<string>();
        void Flush()
        {
            while (current.Count > 0 && current[^1].Length == 0) current.RemoveAt(current.Count - 1);
            pages.Add(new DocumentPage(pages.Count + 1, current.ToArray()));
            current = [];
        }

        foreach (var raw in text.Split('\n'))
        {
            var segments = raw.Split('\f');
            for (var i = 0; i < segments.Length; i++)
            {
                if (i > 0) Flush();
                var line = segments[i].TrimEnd();
                if (line.Trim().Equals(PageBreak, StringComparison.OrdinalIgnoreCase)) Flush();
                else current.Add(line);
            }
        }

        Flush();
        return new DocumentText(pages);
    }
}
