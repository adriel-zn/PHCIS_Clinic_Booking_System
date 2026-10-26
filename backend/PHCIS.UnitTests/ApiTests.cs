using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PHCIS.Application.Models;
using PHCIS.Application.Services;

namespace PHCIS.UnitTests;

public sealed class ApiTests
{
    [Fact]
    public async Task HttpFlowListsBooksCancelsAndRestoresAvailability()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());
        var available = await client.GetAsync(AvailabilityUrl(factory.ClinicId));
        Assert.True(available.StatusCode == HttpStatusCode.OK, factory.LastError?.ToString());
        Assert.Single((await Json(available)).GetProperty("items").EnumerateArray());

        var booked = await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var appointment = await Json(booked);
        var id = appointment.GetProperty("id").GetGuid();
        Assert.Equal(factory.PatientId, appointment.GetProperty("patientId").GetGuid());
        Assert.Equal("Scheduled", appointment.GetProperty("status").GetString());

        var occupied = await client.GetAsync(AvailabilityUrl(factory.ClinicId));
        Assert.Empty((await Json(occupied)).GetProperty("items").EnumerateArray());

        var duplicate = await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        await AssertProblem(duplicate, HttpStatusCode.Conflict);

        var cancelled = await client.PostAsJsonAsync($"/api/appointments/{id}/cancellation",
            new { reason = " Cannot attend " });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancellation = await Json(cancelled);
        Assert.Equal("Cancelled", cancellation.GetProperty("status").GetString());
        Assert.Equal("Cannot attend", cancellation.GetProperty("cancellationReason").GetString());
        Assert.Equal(DomainTestData.Now, cancellation.GetProperty("cancelledAt").GetDateTimeOffset());

        var repeated = await client.PostAsJsonAsync($"/api/appointments/{id}/cancellation",
            new { reason = "Cannot attend" });
        await AssertProblem(repeated, HttpStatusCode.Conflict);

        var restored = await client.GetAsync(AvailabilityUrl(factory.ClinicId));
        Assert.Single((await Json(restored)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task OtherPatientCannotCancelAppointment()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var owner = factory.Client(factory.Token());
        var booked = await owner.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        var id = (await Json(booked)).GetProperty("id").GetGuid();
        using var other = factory.Client(factory.Token(Guid.NewGuid().ToString()));

        var response = await other.PostAsJsonAsync($"/api/appointments/{id}/cancellation",
            new { reason = "Cannot attend" });

        await AssertProblem(response, HttpStatusCode.NotFound);
        var stillOccupied = await owner.GetAsync(AvailabilityUrl(factory.ClinicId));
        Assert.Empty((await Json(stillOccupied)).GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("emptyId")]
    [InlineData("missingId")]
    [InlineData("spoofedPatient")]
    [InlineData("malformedJson")]
    [InlineData("malformedGuid")]
    public async Task InvalidBookingBodiesReturnValidationProblem(string scenario)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        var body = scenario switch
        {
            "emptyId" => "{\"timeSlotId\":\"00000000-0000-0000-0000-000000000000\"}",
            "missingId" => "{}",
            "spoofedPatient" => JsonSerializer.Serialize(new { timeSlotId = factory.SlotId, patientId = Guid.NewGuid() }),
            "malformedGuid" => "{\"timeSlotId\":\"invalid\"}",
            _ => "{"
        };

        var response = await client.PostAsync("/api/appointments",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task CancellationRejectsMissingReason(string? reason)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());

        var response = await client.PostAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/cancellation", new { reason });

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task CancellationRejectsOversizedReason()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        var response = await client.PostAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/cancellation",
            new { reason = new string('x', 501) });
        await AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=invalid&to=2026-10-27T00:00:00Z")]
    [InlineData("?from=2026-10-27T00:00:00Z&to=2026-10-26T00:00:00Z")]
    [InlineData("?from=2026-10-26T00:00:00Z&to=2026-10-27T00:00:00Z&limit=101")]
    [InlineData("?from=2026-10-26T00:00:00Z&to=2026-10-27T00:00:00Z&offset=-1")]
    public async Task InvalidAvailabilityQueriesReturnValidationProblem(string query)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());

        var response = await client.GetAsync($"/api/clinics/{factory.ClinicId}/available-slots{query}");

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task MissingResourcesReturnProblemDetails()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client(factory.Token());

        await AssertProblem(await client.PostAsJsonAsync("/api/appointments",
            new { timeSlotId = Guid.NewGuid() }), HttpStatusCode.NotFound);
        await AssertProblem(await client.PostAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/cancellation",
            new { reason = "Cannot attend" }), HttpStatusCode.NotFound);
        await AssertProblem(await client.GetAsync(AvailabilityUrl(Guid.NewGuid())), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/appointments/not-a-guid/cancellation")]
    [InlineData("/api/no-such-route")]
    public async Task InvalidRoutesReturnNotFoundProblem(string path)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());

        await AssertProblem(await client.PostAsJsonAsync(path, new { reason = "Cannot attend" }),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnsupportedContentTypeReturnsProblemDetails()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        var response = await client.PostAsync("/api/appointments", new StringContent("not json"));
        await AssertProblem(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task UnexpectedExceptionIsRedactedEvenInDevelopment()
    {
        await using var baseFactory = new ApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAvailabilityService>();
            services.AddScoped<IAvailabilityService, FailingAvailabilityService>();
        }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PHCIS=" + baseFactory.Token());

        var response = await client.GetAsync(AvailabilityUrl(Guid.NewGuid()));
        var problem = await AssertProblem(response, HttpStatusCode.InternalServerError);

        Assert.DoesNotContain("secret-patient-data", problem.GetRawText());
        Assert.DoesNotContain("Exception", problem.GetRawText());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnsupportedAcceptHeaderDoesNotBreakProblemResponses(bool knownEndpoint)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");

        var response = knownEndpoint
            ? await client.GetAsync(AvailabilityUrl(Guid.NewGuid()))
            : await client.GetAsync("/api/no-such-route");

        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    private sealed class FailingAvailabilityService : IAvailabilityService
    {
        public Task<AvailableSlotsDto> GetAvailableSlotsAsync(
            AvailableSlotsRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("secret-patient-data database-password");
    }

    internal static string AvailabilityUrl(Guid clinicId) =>
        $"/api/clinics/{clinicId}/available-slots?from=2026-10-26T00:00:00Z&to=2026-10-27T00:00:00Z";

    internal static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await Json(response);
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("traceId", out _));
        return body;
    }

    internal static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
}

