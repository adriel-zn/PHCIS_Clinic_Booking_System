using System.Net;

namespace PHCIS.UnitTests;

public sealed class SwaggerTests
{
    [Fact]
    public async Task SwaggerDocumentsEndpointsSchemasResponsesAndCookieSecurity()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await ApiTests.Json(response);
        var paths = document.GetProperty("paths");
        var booking = paths.GetProperty("/api/appointments").GetProperty("post");
        Assert.True(booking.GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(booking.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(booking.GetProperty("responses").TryGetProperty("409", out _));
        Assert.True(paths.TryGetProperty("/api/appointments/{appointmentId}/cancellation", out _));
        Assert.True(paths.TryGetProperty("/api/clinics/{clinicId}/available-slots", out _));
        Assert.True(document.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("session", out _));
        Assert.Contains("session", booking.GetProperty("security").GetRawText());
        Assert.DoesNotContain("patientId", document.GetProperty("components").GetProperty("schemas")
            .GetProperty("BookAppointmentBody").GetProperty("properties").GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(booking.GetProperty("summary").GetString()));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task SwaggerIsNotExposedOutsideDevelopment()
    {
        await using var factory = new ApiFactory("Production");
        using var client = factory.Client();
        await ApiTests.AssertProblem(await client.GetAsync("/swagger/v1/swagger.json"), HttpStatusCode.NotFound);
        await ApiTests.AssertProblem(await client.GetAsync("/swagger/index.html"), HttpStatusCode.NotFound);
    }
}

