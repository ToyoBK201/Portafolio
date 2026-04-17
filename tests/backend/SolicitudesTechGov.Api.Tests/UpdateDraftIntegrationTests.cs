using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SolicitudesTechGov.Api.Tests;

public sealed class UpdateDraftIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ValidSoftwarePayloadJson =
        """{"productName":"Office","licenseModel":"Subscription","seatOrUserCount":10,"environment":"Production"}""";

    private const string Desc20 =
        "Descripción mínima veinte caracteres.";

    private const string Just20 =
        "Justificación operativa mínima 20 caracteres.";

    private readonly TestWebApplicationFactory _factory;

    public UpdateDraftIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PatchDraft_UpdatesTitle_WhenRequesterAndDraft()
    {
        var client = _factory.CreateClient();

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud PATCH borrador",
            description = Desc20,
            businessJustification = Just20,
            requestType = 2,
            priority = 2,
            requestingUnitId = 3,
            requesterUserId = userId,
            desiredDate = (string?)null,
            specificPayloadJson = ValidSoftwarePayloadJson
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedStub>();
        Assert.NotNull(created);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}",
            new
            {
                title = "Solicitud PATCH borrador actualizada",
                description = Desc20,
                businessJustification = Just20,
                requestType = (byte)2,
                priority = (byte)2,
                requestingUnitId = 3,
                desiredDate = (string?)null,
                specificPayloadJson = ValidSoftwarePayloadJson
            });

        patchResponse.EnsureSuccessStatusCode();
        var updated = await patchResponse.Content.ReadFromJsonAsync<CreatedStub>();
        Assert.NotNull(updated);
        Assert.Equal("Solicitud PATCH borrador actualizada", updated!.Title);
    }

    [Fact]
    public async Task PatchDraft_Returns403_WhenTicAnalyst()
    {
        var client = _factory.CreateClient();

        var ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tokenOwner = await GetDevTokenAsync(client, ownerId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwner);

        var createResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Solicitud para PATCH denegado",
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
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedStub>();
        Assert.NotNull(created);

        var analystId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tokenAnalyst = await GetDevTokenAsync(client, analystId, "TicAnalyst");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAnalyst);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/v1/requests/{created!.RequestId}",
            new
            {
                title = "Intento no autorizado",
                description = Desc20,
                businessJustification = Just20,
                requestType = (byte)2,
                priority = (byte)2,
                requestingUnitId = 3,
                desiredDate = (string?)null,
                specificPayloadJson = ValidSoftwarePayloadJson
            });

        Assert.Equal(HttpStatusCode.Forbidden, patchResponse.StatusCode);
    }

    private static async Task<string> GetDevTokenAsync(HttpClient client, Guid userId, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/dev-token", new { userId, role });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString() ?? throw new InvalidOperationException();
    }

    private sealed record CreatedStub(
        [property: JsonPropertyName("requestId")] Guid RequestId,
        [property: JsonPropertyName("title")] string Title);
}
