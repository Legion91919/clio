using System.IO.Compression;
using System.Text;

namespace Clio.Support.Tests;

/// <summary>Writes tiny text-layer PDFs (one Helvetica font, one content stream per page) for tests.</summary>
public static class PdfFixture
{
    public static byte[] Build(IReadOnlyList<string[]> pages, bool compress = true, bool textLayer = true)
    {
        var objects = new List<byte[]>();
        var pageIds = new List<int>();
        int nextId = 3 + 1; // 1 catalog, 2 pages, 3 font
        var bodies = new List<(int Id, byte[] Body)>();
        foreach (var lines in pages)
        {
            int contentId = nextId++, pageId = nextId++;
            var sb = new StringBuilder("BT /F1 11 Tf 14 TL 72 760 Td\n");
            if (textLayer)
            {
                foreach (var line in lines) sb.Append('(').Append(line.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal)).Append(") Tj T*\n");
            }
            else
            {
                sb.Clear().Append("q 600 0 0 800 0 0 cm /Im1 Do Q\n");
            }

            sb.Append(textLayer ? "ET\n" : string.Empty);
            var raw = Encoding.Latin1.GetBytes(sb.ToString());
            var data = compress ? Deflate(raw) : raw;
            var dict = Encoding.ASCII.GetBytes($"<< /Length {data.Length}{(compress ? " /Filter /FlateDecode" : string.Empty)} >>\nstream\n");
            bodies.Add((contentId, [.. dict, .. data, .. "\nendstream"u8]));
            bodies.Add((pageId, Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>")));
            pageIds.Add(pageId);
        }

        bodies.Insert(0, (1, "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray()));
        bodies.Insert(1, (2, Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{string.Join(' ', pageIds.Select(p => $"{p} 0 R"))}] /Count {pageIds.Count} >>")));
        bodies.Insert(2, (3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"u8.ToArray()));

        using var ms = new MemoryStream();
        ms.Write("%PDF-1.4\n"u8);
        foreach (var (id, body) in bodies.OrderBy(b => b.Id))
        {
            ms.Write(Encoding.ASCII.GetBytes($"{id} 0 obj\n"));
            ms.Write(body);
            ms.Write("\nendobj\n"u8);
        }

        ms.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Root 1 0 R /Size {bodies.Count + 1} >>\n%%EOF\n"));
        _ = objects;
        return ms.ToArray();
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        return ms.ToArray();
    }
}
