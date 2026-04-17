using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SolicitudesTechGov.Api.Tests;

public sealed class AdminCatalogIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdminCatalogIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrganizationalUnits_Returns200_ForRequester()
    {
        var client = _factory.CreateClient();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/catalogs/organizational-units?activeOnly=false");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PostOrganizationalUnit_Returns403_ForRequester()
    {
        var client = _factory.CreateClient();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = await GetDevTokenAsync(client, userId, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.PostAsJsonAsync(
            "/api/v1/admin/organizational-units",
            new { code = "X99", name = "Unidad de prueba API" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationalUnit_Returns201_ForAdmin()
    {
        var client = _factory.CreateClient();
        var adminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var token = await GetDevTokenAsync(client, adminId, "SystemAdministrator");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.PostAsJsonAsync(
            "/api/v1/admin/organizational-units",
            new { code = "ADM-UNIT-" + Guid.NewGuid().ToString("N")[..8], name = "Creada por test admin" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task PutUserRoles_Returns204_ForAdmin()
    {
        var client = _factory.CreateClient();
        var adminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var token = await GetDevTokenAsync(client, adminId, "SystemAdministrator");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var target = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var res = await client.PutAsJsonAsync(
            $"/api/v1/admin/users/{target}/roles",
            new
            {
                assignments = new[]
                {
                    new { roleId = (byte)1, organizationalUnitId = (int?)null },
                    new { roleId = (byte)3, organizationalUnitId = (int?)null }
                }
            });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    private static async Task<string> GetDevTokenAsync(HttpClient client, Guid userId, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/dev-token", new { userId, role });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString() ?? throw new InvalidOperationException();
    }
}
