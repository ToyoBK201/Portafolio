using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SolicitudesTechGov.Api.Tests;

public sealed class ExportFormatIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ExportFormatIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Export_Xlsx_Returns200_AndContentType()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/requests/export?format=xlsx&maxRows=100");
        res.EnsureSuccessStatusCode();
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            res.Content.Headers.ContentType?.MediaType);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 64);
        // ZIP / OOXML: PK header
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task Export_InvalidFormat_Returns400_WithCorrelation()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/requests/export?format=pdf");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
        Assert.True(doc.RootElement.TryGetProperty("detail", out _));
    }

    private static async Task<string> GetDevTokenAsync(HttpClient client, Guid userId, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/dev-token", new { userId, role });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString() ?? throw new InvalidOperationException();
    }
}
