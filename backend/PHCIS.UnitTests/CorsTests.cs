using System.Net;

namespace PHCIS.UnitTests;

public sealed class CorsTests
{
    [Theory]
    [InlineData("https://localhost:7100", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task PreflightAllowsOnlyConfiguredFrontendOrigins(string origin, bool allowed)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/appointments");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        }
    }

    [Fact]
    public async Task CorsDoesNotBypassAuthentication()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiTests.AvailabilityUrl(factory.ClinicId));
        request.Headers.Add("Origin", "https://localhost:7100");

        using var response = await client.SendAsync(request);

        await ApiTests.AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Equal("https://localhost:7100", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}

