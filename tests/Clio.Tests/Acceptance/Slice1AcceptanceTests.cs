using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Clio.Support.Tests;
using Xunit;

namespace Clio.Acceptance.Tests;

/// <summary>One acceptance test per slice-1 criterion, over real HTTP with seeded invented users.</summary>
public class Slice1AcceptanceTests
{
    private const string Analyst = "analyst@example.com";
    private const string Committee = "committee@example.com";

    private const string ClearSheet = """
        INDICATIVE TERM SHEET
        Borrower: Kestrel Bay Logistics (Pty) Ltd (invented)
        Facility: Senior secured term loan
        Facility Amount: ZAR 48,500,000
        Margin: 2.85% per annum
        Final Maturity Date: 30 June 2031
        """;

    private const string UnsureMarginSheet = """
        TERM SHEET
        Facility Amount: ZAR 15,000,000
        The loan bears interest at a margin of 2.6% above prime.
        Final Maturity Date: 28 February 2030
        """;

    [Fact]
    public async Task Given_a_clear_term_sheet_when_uploaded_then_amount_margin_and_maturity_are_extracted_with_confidence_of_at_least_0_9()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var upload = await host.UploadTextAsync(token, "kestrel.txt", ClearSheet);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var doc = await host.WaitForProcessedAsync(token, id);
        Assert.Equal("Complete", doc.GetProperty("status").GetString());
        foreach (var (name, value, line) in new[] { ("LoanAmount", "ZAR 48500000.00", 4), ("Margin", "2.85", 5), ("Maturity", "2031-06-30", 6) })
        {
            var field = doc.Field(name);
            Assert.Equal(value, field.GetProperty("value").GetString());
            Assert.True(field.GetProperty("confidence").GetDecimal() >= 0.9m);
            Assert.Equal(1, field.GetProperty("source").GetProperty("page").GetInt32());
            Assert.Equal(line, field.GetProperty("source").GetProperty("line").GetInt32());
            Assert.Equal("AutoAccepted", field.GetProperty("status").GetString());
        }

        var page = await host.GetJsonAsync(token, $"/api/documents/{id}/pages/1");
        Assert.Equal("Margin: 2.85% per annum", page.GetProperty("lines")[4].GetString());
    }

    [Fact]
    public async Task Given_a_clear_text_layer_pdf_when_uploaded_then_the_three_terms_are_extracted_with_their_page()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var pdf = PdfFixture.Build([["INDICATIVE TERM SHEET", "Borrower: Amberfield Farms (invented)"], ["Loan Amount: USD 3,000,000.00", "Margin: 3.10%", "Maturity Date: 2030-03-31"]]);
        var upload = await host.UploadAsync(token, "amberfield.pdf", pdf);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var doc = await host.WaitForProcessedAsync(token, id);
        Assert.Equal("Complete", doc.GetProperty("status").GetString());
        Assert.Equal(2, doc.Field("Margin").GetProperty("source").GetProperty("page").GetInt32());
        Assert.Equal("USD 3000000.00", doc.Field("LoanAmount").GetProperty("value").GetString());
    }

    [Fact]
    public async Task Given_a_field_below_the_threshold_then_it_is_shown_in_the_review_queue_not_saved()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var upload = await host.UploadTextAsync(token, "unsure.txt", UnsureMarginSheet);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var doc = await host.WaitForProcessedAsync(token, id);

        Assert.Equal("NeedsReview", doc.GetProperty("status").GetString());
        Assert.Equal("NeedsReview", doc.Field("Margin").GetProperty("status").GetString());
        Assert.False(doc.GetProperty("terms").TryGetProperty("Margin", out _));

        var queue = await host.GetJsonAsync(token, "/api/review-queue");
        Assert.Contains(queue.EnumerateArray(), item => item.GetProperty("documentId").GetString() == id && item.GetProperty("field").GetString() == "Margin");
    }

    [Fact]
    public async Task Given_an_approved_field_then_its_source_page_and_the_approving_user_are_stored()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var me = await host.GetJsonAsync(token, "/auth/me");
        var upload = await host.UploadTextAsync(token, "unsure.txt", UnsureMarginSheet);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var before = await host.WaitForProcessedAsync(token, id);
        var line = before.Field("Margin").GetProperty("source").GetProperty("line").GetInt32();
        var others = before.GetProperty("fields").EnumerateArray().Where(f => f.GetProperty("status").GetString() == "NeedsReview").Select(f => f.GetProperty("field").GetString()!).ToList();

        var approve = await host.SendAsync(HttpMethod.Post, $"/api/documents/{id}/fields/Margin/approve", new { value = "2.60" }, token);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var doc = await host.GetJsonAsync(token, $"/api/documents/{id}");
        var margin = doc.Field("Margin");
        Assert.Equal("Approved", margin.GetProperty("status").GetString());
        Assert.Equal(me.GetProperty("id").GetString(), margin.GetProperty("approvedBy").GetString());
        Assert.Equal(1, margin.GetProperty("source").GetProperty("page").GetInt32());
        Assert.Equal(line, margin.GetProperty("source").GetProperty("line").GetInt32());
        Assert.Equal("2.60", doc.GetProperty("terms").GetProperty("Margin").GetString());

        var committee = await host.LoginAsync(Committee);
        var audit = await host.GetJsonAsync(committee, $"/audit/entities/term-field/{id}:Margin");
        var entry = audit.GetProperty("items")[0];
        Assert.Equal("field.approved", entry.GetProperty("action").GetString());
        Assert.Equal(me.GetProperty("id").GetString(), entry.GetProperty("actor").GetString());

        foreach (var other in others.Where(o => o != "Margin"))
        {
            var current = await host.GetJsonAsync(token, $"/api/documents/{id}");
            var value = current.Field(other).GetProperty("value").GetString();
            await host.SendAsync(HttpMethod.Post, $"/api/documents/{id}/fields/{other}/approve", new { value }, token);
        }

        var final = await host.GetJsonAsync(token, $"/api/documents/{id}");
        Assert.Equal("Complete", final.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Given_an_unreadable_file_then_the_analyst_sees_a_clear_error_and_the_original_is_kept()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var original = PdfFixture.Build([["no text here"]], textLayer: false);
        var upload = await host.UploadAsync(token, "photo-scan.pdf", original);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var doc = await host.WaitForProcessedAsync(token, id);
        Assert.Equal("Unreadable", doc.GetProperty("status").GetString());
        Assert.Equal("document.unreadable", doc.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("text layer", doc.GetProperty("error").GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);

        var kept = await host.SendAsync(HttpMethod.Get, $"/api/documents/{id}/original", bearer: token);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(original, await kept.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Uploading_the_same_file_twice_returns_the_same_document()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var first = await host.UploadTextAsync(token, "a.txt", ClearSheet);
        var second = await host.UploadTextAsync(token, "a-copy.txt", ClearSheet);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString(),
            (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString());
    }

    [Fact]
    public async Task Committee_can_read_but_not_upload_or_approve_and_anonymous_users_are_refused()
    {
        await using var host = await TestApp.StartAsync();
        var committee = await host.LoginAsync(Committee);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.UploadTextAsync(committee, "x.txt", ClearSheet)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(HttpMethod.Get, "/api/documents", bearer: committee)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/documents")).StatusCode);
    }

    [Fact]
    public async Task Empty_upload_is_rejected_with_problem_details()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var response = await host.UploadAsync(token, "empty.txt", []);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Completed_terms_are_handed_to_screening_through_the_outbox()
    {
        await using var host = await TestApp.StartAsync();
        var token = await host.LoginAsync(Analyst);
        var upload = await host.UploadTextAsync(token, "kestrel.txt", ClearSheet);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        await host.WaitForProcessedAsync(token, id);
        for (var i = 0; i < 100; i++)
        {
            var handoffs = await host.GetJsonAsync(token, "/api/screening/handoffs");
            if (handoffs.EnumerateArray().Any(h => h.GetProperty("documentId").GetString() == id)) return;
            await Task.Delay(50);
        }

        Assert.True(false, "No screening hand-off arrived.");
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        await using var host = await TestApp.StartAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/ready")).StatusCode);
        _ = Encoding.UTF8;
    }
}
