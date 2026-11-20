using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Agentic.Blocks.Testing;
using Clio.Api;

namespace Clio.Support.Tests;

public static class TestApp
{
    public const string DemoSecret = "test-only-demo-secret-42";

    public static Task<KestrelTestHost> StartAsync() => KestrelTestHost.StartAsync(
        b =>
        {
            b.Configuration["Demo:Password"] = DemoSecret;
            b.Configuration["Seed:SamplesPath"] = string.Empty;
            ClioApp.ConfigureServices(b);
        },
        ClioApp.Configure);

    public static async Task<string> LoginAsync(this KestrelTestHost host, string email)
    {
        var response = await host.Client.PostAsJsonAsync("/auth/login", new Dictionary<string, string> { ["email"] = email, ["password"] = DemoSecret });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    public static async Task<HttpResponseMessage> UploadAsync(this KestrelTestHost host, string token, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(fileName.EndsWith(".pdf", StringComparison.Ordinal) ? "application/pdf" : "text/plain");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await host.Client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> UploadTextAsync(this KestrelTestHost host, string token, string fileName, string text) =>
        host.UploadAsync(token, fileName, Encoding.UTF8.GetBytes(text));

    public static async Task<JsonElement> GetJsonAsync(this KestrelTestHost host, string token, string path)
    {
        var response = await host.SendAsync(HttpMethod.Get, path, bearer: token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Polls until the worker has finished with the document.</summary>
    public static async Task<JsonElement> WaitForProcessedAsync(this KestrelTestHost host, string token, string id)
    {
        for (var i = 0; i < 100; i++)
        {
            var doc = await host.GetJsonAsync(token, $"/api/documents/{id}");
            var status = doc.GetProperty("status").GetString();
            if (status is not ("Received" or "Processing")) return doc;
            await Task.Delay(50);
        }

        throw new TimeoutException("Document was not processed in time.");
    }

    public static JsonElement Field(this JsonElement doc, string name) =>
        doc.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("field").GetString() == name);
}
