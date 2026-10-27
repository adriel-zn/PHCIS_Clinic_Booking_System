using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PHCIS.Web.Configuration;
using PHCIS.Web.Models;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task SearchEncodesClinicLocalDayAndPaging()
    {
        Uri? uri = null;
        var handler = new StubHandler(request =>
        {
            uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AvailableSlotsPage([], 12, 12, false))
            });
        });
        var client = Client(handler);

        await client.FindAvailableAsync(new AvailabilityQuery(
            FakeAppointmentApiClient.ClinicId, new DateOnly(2026, 10, 26), 12, 12), default);

        var path = Uri.UnescapeDataString(uri!.AbsoluteUri);
        Assert.Contains($"/api/clinics/{FakeAppointmentApiClient.ClinicId}/available-slots", path);
        Assert.Contains("from=2026-10-26T00:00:00.0000000+02:00", path);
        Assert.Contains("to=2026-10-27T00:00:00.0000000+02:00", path);
        Assert.Contains("offset=12&limit=12", path);
        Assert.Contains("%2B02%3A00", uri.Query);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritesSendOnlyApiContractFieldsNeverPatientIdentity(bool cancel)
    {
        string? body = null;
        string? path = null;
        var handler = new StubHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            path = request.RequestUri!.AbsolutePath;
            Assert.Equal(HttpMethod.Post, request.Method);
            return new HttpResponseMessage(cancel ? HttpStatusCode.OK : HttpStatusCode.Created)
            {
                Content = JsonContent.Create(FakeAppointmentApiClient.Appointment())
            };
        });
        var client = Client(handler);
        var id = Guid.NewGuid();

        if (cancel)
        {
            await client.CancelAsync(id, " Cannot attend ", default);
            Assert.Equal($"/api/appointments/{id}/cancellation", path);
            Assert.Equal("Cannot attend", JsonSerializer.Deserialize<JsonElement>(body!).GetProperty("reason").GetString());
        }
        else
        {
            await client.BookAsync(id, default);
            Assert.Equal("/api/appointments", path);
            Assert.Equal(id, JsonSerializer.Deserialize<JsonElement>(body!).GetProperty("timeSlotId").GetGuid());
        }
        Assert.DoesNotContain("patient", body!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cancel ? 1 : 2, JsonSerializer.Deserialize<JsonElement>(body!).EnumerateObject().Count());
    }

    [Fact]
    public async Task ValidationProblemPreservesFieldErrorsAndTrace()
    {
        var client = Client(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new
            {
                detail = "Invalid data",
                traceId = "trace-1",
                errors = new Dictionary<string, string[]> { ["Reason"] = ["Too long."] }
            })
        })));

        var error = await Assert.ThrowsAsync<AppointmentApiException>(() =>
            client.CancelAsync(Guid.NewGuid(), "reason", default));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("trace-1", error.TraceId);
        Assert.Equal("Too long.", Assert.Single(error.Errors["Reason"]));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task SensitiveServerDetailsAreNotDisplayedForAuthenticationOrServerErrors(int status)
    {
        var client = Client(new StubHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = JsonContent.Create(new { detail = "database-password secret", traceId = "trace-2" })
        })));

        var error = await Assert.ThrowsAsync<AppointmentApiException>(() => client.BookAsync(Guid.NewGuid(), default));

        Assert.Equal(status, error.StatusCode);
        Assert.DoesNotContain("database-password", error.Message);
        Assert.Equal("trace-2", error.TraceId);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(409)]
    public async Task KnownProblemDetailsHaveActionableMessage(int status)
    {
        var client = Client(new StubHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = JsonContent.Create(new { detail = "The slot is no longer available.", traceId = "trace-3" })
        })));

        var error = await Assert.ThrowsAsync<AppointmentApiException>(() => client.BookAsync(Guid.NewGuid(), default));

        Assert.Equal("The slot is no longer available.", error.Message);
    }

    [Fact]
    public async Task HtmlProxyErrorIsNeverDisplayed()
    {
        var client = Client(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>proxy-secret</html>", Encoding.UTF8, "text/html")
        })));

        var error = await Assert.ThrowsAsync<AppointmentApiException>(() => client.BookAsync(Guid.NewGuid(), default));

        Assert.DoesNotContain("proxy-secret", error.Message);
    }

    [Fact]
    public async Task NetworkFailureDoesNotRetryWriteAndWarnsOfUncertainOutcome()
    {
        var calls = 0;
        var client = Client(new StubHandler(_ =>
        {
            calls++;
            throw new HttpRequestException("private network details");
        }));

        var error = await Assert.ThrowsAsync<AppointmentApiException>(() => client.BookAsync(Guid.NewGuid(), default));

        Assert.Equal(1, calls);
        Assert.Contains("could not confirm", error.Message);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task CallerCancellationPropagatesInsteadOfBecomingDisplayError()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var client = Client(new StubHandler(_ => throw new OperationCanceledException(source.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.BookAsync(Guid.NewGuid(), source.Token));
    }

    [Fact]
    public async Task UnreadableSuccessWarnsWriteMayHaveSucceeded()
    {
        var client = Client(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{")
        })));

        var error = await Assert.ThrowsAsync<AppointmentApiException>(() => client.BookAsync(Guid.NewGuid(), default));

        Assert.Contains("may have succeeded", error.Message);
    }

    private static AppointmentApiClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, new PortalOptions());
}

internal sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request);
}

