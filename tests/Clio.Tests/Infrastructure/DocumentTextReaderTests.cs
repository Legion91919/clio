using System.Text;
using Clio.Infrastructure;
using Clio.Support.Tests;
using Xunit;

namespace Clio.Infrastructure.Tests;

public class DocumentTextReaderTests
{
    private readonly CompositeDocumentTextReader _reader = new();

    [Fact]
    public void Plain_text_is_split_into_pages_and_numbered_lines()
    {
        var text = "TERM SHEET\nFacility Amount: ZAR 1,000,000\n--- page break ---\nMargin: 2.5%\fMaturity Date: 2031-01-31";
        var result = _reader.Read(Encoding.UTF8.GetBytes(text), "ts.txt");
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Pages.Count);
        Assert.Equal("Facility Amount: ZAR 1,000,000", result.Value.Pages[0].Lines[1]);
        Assert.Equal(2, result.Value.Pages[1].Number);
        Assert.Equal("Margin: 2.5%", result.Value.Pages[1].Lines[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Text_layer_pdf_is_read_page_by_page(bool compress)
    {
        var pdf = PdfFixture.Build([["TERM SHEET", "Facility Amount: ZAR 2,000,000 (two million)"], ["Margin: 3.25%"]], compress);
        var result = _reader.Read(pdf, "ts.pdf");
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Pages.Count);
        Assert.Equal("Facility Amount: ZAR 2,000,000 (two million)", result.Value.Pages[0].Lines[1]);
        Assert.Equal("Margin: 3.25%", result.Value.Pages[1].Lines[0]);
    }

    [Fact]
    public void Pdf_without_a_text_layer_is_unreadable_with_a_clear_message()
    {
        var result = _reader.Read(PdfFixture.Build([["ignored"]], textLayer: false), "scan.pdf");
        Assert.False(result.IsSuccess);
        Assert.Equal("document.unreadable", result.Error.Code);
        Assert.Contains("text layer", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("scan.pdf")]
    [InlineData("photo.jpg")]
    public void Binary_or_corrupt_files_are_unreadable(string name)
    {
        var bytes = name.EndsWith(".pdf", StringComparison.Ordinal) ? Encoding.ASCII.GetBytes("%PDF-1.4\n garbage without objects") : new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02, 0x03, 0x00 };
        var result = _reader.Read(bytes, name);
        Assert.False(result.IsSuccess);
        Assert.Equal("document.unreadable", result.Error.Code);
    }
}
