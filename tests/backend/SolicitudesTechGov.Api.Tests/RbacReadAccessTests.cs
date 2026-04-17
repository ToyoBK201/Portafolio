using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SolicitudesTechGov.Api.Tests;

public sealed class RbacReadAccessTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ValidSoftwarePayloadJson =
        """{"productName":"Office","licenseModel":"Subscription","seatOrUserCount":10,"environment":"Production"}""";

    private const string Desc20 =
        "Descripción mínima veinte caracteres.";

    private const string Just20 =
        "Justificación operativa mínima 20 caracteres.";

    private readonly TestWebApplicationFactory _factory;

    public RbacReadAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRequest_TicAnalyst_CanReadOtherUsersRequest()
    {
        var client = _factory.CreateClient();

        var ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tokenOwner = await GetDevTokenAsync(client, ownerId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwner);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud RBAC lectura",
            description = Desc20,
            businessJustification = Just20,
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = ownerId,
            desiredDate = (string?)null,
            specificPayloadJson = ValidSoftwarePayloadJson
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var analystId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tokenAnalyst = await GetDevTokenAsync(client, analystId, "TicAnalyst");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAnalyst);

        var getResponse = await client.GetAsync($"/api/v1/requests/{created!.RequestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetRequest_Requester_ForbiddenForOtherOwnersRequest()
    {
        var client = _factory.CreateClient();

        var ownerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tokenA = await GetDevTokenAsync(client, ownerA, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud de A",
            description = Desc20,
            businessJustification = Just20,
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = ownerA,
            desiredDate = (string?)null,
            specificPayloadJson = ValidSoftwarePayloadJson
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedRequestStub>();
        Assert.NotNull(created);

        var ownerB = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var tokenB = await GetDevTokenAsync(client, ownerB, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var getResponse = await client.GetAsync($"/api/v1/requests/{created!.RequestId}");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
    }

    [Fact]
    public async Task WorkQueueSummary_ReturnsCounts()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/me/work-queue-summary");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("countsByStatus", out var counts));
        Assert.Equal(JsonValueKind.Object, counts.ValueKind);
    }

    private static async Task<string> GetDevTokenAsync(HttpClient client, Guid userId, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/dev-token", new { userId, role });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString() ?? throw new InvalidOperationException();
    }

    private sealed record CreatedRequestStub([property: JsonPropertyName("requestId")] Guid RequestId);
}
