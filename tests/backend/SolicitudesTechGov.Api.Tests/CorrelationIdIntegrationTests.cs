using Xunit;

namespace SolicitudesTechGov.Api.Tests;

public sealed class CorrelationIdIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Returns_X_Correlation_Id_Header()
    {
        var response = await _client.GetAsync("/api/v1/health");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.True(Guid.TryParse(values.First(), out _));
    }

    [Fact]
    public async Task Health_Echoes_Incoming_Correlation_Id()
    {
        var expected = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", expected.ToString());

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(expected, Guid.Parse(values.First()));
    }
}
