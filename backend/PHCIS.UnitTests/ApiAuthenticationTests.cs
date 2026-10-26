using System.Net;
using System.Net.Http.Json;

namespace PHCIS.UnitTests;

public sealed class ApiAuthenticationTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("malformed")]
    [InlineData("signature")]
    [InlineData("expired")]
    public async Task MissingOrInvalidCookieReturnsUnauthorized(string scenario)
    {
        await using var factory = new ApiFactory();
        var token = scenario switch
        {
            "none" => null,
            "malformed" => "not-a-cookie",
            "signature" => factory.Token(wrongSignature: true),
            _ => factory.Token(expired: true)
        };
        using var client = factory.Client(token);
        await ApiTests.AssertProblem(await client.PostAsJsonAsync("/api/appointments",
            new { timeSlotId = factory.SlotId }), HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("empty")]
    [InlineData("duplicate")]
    public async Task CookieWithoutUnambiguousPatientIdentityReturnsForbidden(string scenario)
    {
        await using var factory = new ApiFactory();
        var token = factory.Token(
            patientClaim: scenario == "empty" ? Guid.Empty.ToString() : scenario == "invalid" ? "invalid" : null,
            omitPatientClaim: scenario == "missing", duplicatePatientClaim: scenario == "duplicate");
        using var client = factory.Client(token);
        await ApiTests.AssertProblem(await client.GetAsync(ApiTests.AvailabilityUrl(factory.ClinicId)),
            HttpStatusCode.Forbidden);
    }
}

