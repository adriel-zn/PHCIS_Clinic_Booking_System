using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Components;
using PHCIS.Web.Configuration;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class ComponentTests : BunitContext
{
    private readonly FakeAppointmentApiClient _api = new();

    public ComponentTests()
    {
        Services.AddSingleton<IAppointmentApiClient>(_api);
        Services.AddSingleton(new PortalOptions());
        Services.AddSingleton<TimeProvider>(new FixedClock());
        var http = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = System.Net.Http.Json.JsonContent.Create(Array.Empty<AppointmentDetails>())
        })))
        { BaseAddress = new Uri("https://api.test/") };
        var session = new LocalAuthenticationStateProvider(http);
        Services.AddSingleton(session);
        Services.AddSingleton(new AccountClient(http, session));
        Services.AddSingleton(new PortalClient(http));
    }

    [Fact]
    public void SignedOutHomeOffersSignInWithoutRenderingPatientWorkspace()
    {
        this.AddAuthorization().SetNotAuthorized();

        var page = Render<Home>();

        Assert.Contains("PHCIS", page.Markup);
        Assert.Single(page.FindAll("#email"));
        Assert.Empty(page.FindAll(".availability"));
    }

    [Fact]
    public void SignedInHomeShowsBookingAndCancellationWithoutFetchingBeforeSearch()
    {
        this.AddAuthorization().SetAuthorized("Patient");

        var page = Render<Home>();

        Assert.Single(page.FindAll(".quick-grid"));
        Assert.Equal(0, _api.Searches);
    }

    [Fact]
    public void SearchValidatesBeforeCallingApi()
    {
        var page = Render<AvailabilityPanel>();

        page.Find("form").Submit();

        Assert.Equal(0, _api.Searches);
        Assert.Contains("Enter the clinic reference", page.Markup);
    }

    [Fact]
    public void SearchShowsClinicTimeAndRequiresExplicitBookingConfirmation()
    {
        var page = Render<AvailabilityPanel>();
        Search(page);

        Assert.Equal(1, _api.Searches);
        Assert.Contains("11:00", page.Find(".slot").TextContent);
        Assert.Contains("UTC+02:00", page.Markup);
        Assert.Equal(0, _api.Bookings);

        page.Find(".slot").Click();
        Assert.Equal("true", page.Find(".slot").GetAttribute("aria-pressed"));
        Assert.Equal(0, _api.Bookings);

        page.Find(".booking-review button").Click();
        Assert.Equal(1, _api.Bookings);
        Assert.Empty(page.FindAll(".slot"));
    }

    [Fact]
    public void EditingSearchClearsStaleResultsAndSelectedSlot()
    {
        var page = Render<AvailabilityPanel>();
        Search(page);
        page.Find(".slot").Click();

        page.Find("#visit-date").Change("2026-10-27");

        Assert.Empty(page.FindAll(".slot"));
        Assert.Empty(page.FindAll(".booking-review"));
    }

    [Fact]
    public void EmptyResultsHaveUsefulAccessibleMessage()
    {
        _api.Page = new AvailableSlotsPage([], 0, 12, false);
        var page = Render<AvailabilityPanel>();

        Search(page);

        Assert.Contains("No available slots", page.Find("[role='status']").TextContent);
        Assert.Empty(page.FindAll(".slot"));
    }

    [Fact]
    public void PagingUsesTheOriginalSearchAndClearsSelection()
    {
        _api.Page = new AvailableSlotsPage([FakeAppointmentApiClient.Slot], 0, 12, true);
        var page = Render<AvailabilityPanel>();
        Search(page);
        page.Find(".slot").Click();

        page.Find(".pagination button:last-child").Click();

        Assert.Equal(12, _api.LastQuery!.Offset);
        Assert.Equal(FakeAppointmentApiClient.ClinicId, _api.LastQuery.ClinicId);
        Assert.Empty(page.FindAll(".booking-review"));
    }

    [Fact]
    public void BookingConflictRequiresFreshAvailability()
    {
        var page = Render<AvailabilityPanel>();
        Search(page);
        page.Find(".slot").Click();
        _api.Failure = new AppointmentApiException(409, "This slot is occupied.", "trace-123");

        page.Find(".booking-review button").Click();

        Assert.Contains("This slot is occupied", page.Find("[role='alert']").TextContent);
        Assert.Contains("trace-123", page.Markup);
        Assert.Empty(page.FindAll(".slot"));
        Assert.Empty(page.FindAll(".booking-review"));
    }

    [Fact]
    public async Task PendingBookingDisablesActionsAndCannotBeSubmittedTwice()
    {
        var completion = new TaskCompletionSource<AppointmentSummary>(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.PendingBooking = completion;
        var page = Render<AvailabilityPanel>();
        Search(page);
        page.Find(".slot").Click();

        var first = page.Find(".booking-review button").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.True(page.Find(".booking-review button").HasAttribute("disabled")));
        var second = page.Find(".booking-review button").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, _api.Bookings);
        completion.SetResult(FakeAppointmentApiClient.Appointment());
        await Task.WhenAll(first, second);
        Assert.Equal(1, _api.Bookings);
    }

    [Fact]
    public void CancellationRequiresReferenceReasonAndConfirmation()
    {
        var page = Render<CancellationPanel>();

        page.Find("form").Submit();

        Assert.Equal(0, _api.Cancellations);
        Assert.Contains("Enter a valid appointment reference", page.Markup);
    }

    [Fact]
    public void CancellationRequiresDeliberateConfirmation()
    {
        var page = Render<CancellationPanel>();
        page.Find("#cancel-reference").Change(Guid.NewGuid().ToString());
        page.Find("#cancel-reason").Change("Cannot attend");

        page.Find("form").Submit();

        Assert.Equal(0, _api.Cancellations);
        Assert.Contains("Confirm that you want to cancel", page.Markup);
    }

    [Fact]
    public void BookingReceiptProvidesReferenceAndPrepopulatesCancellation()
    {
        var page = Render<BookingWorkspace>();
        page.Find("#clinic-reference").Change(FakeAppointmentApiClient.ClinicId.ToString());
        page.Find(".availability form").Submit();
        page.Find(".slot").Click();
        page.Find(".booking-review button").Click();
        var reference = page.Find("#appointment-reference").GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(reference));
        Assert.Equal(reference, page.Find("#cancel-reference").GetAttribute("value"));
        Assert.Contains("Your appointment is booked", page.Markup);

        page.Find("#cancel-reason").Change("Cannot attend");
        page.Find("#cancel-confirm").Change(true);
        page.Find(".cancellation form").Submit();

        Assert.Equal(Guid.Parse(reference!), _api.LastCancellation!.Value.Id);
        Assert.Contains("Appointment cancelled", page.Find(".receipt").TextContent);
        Assert.Equal(1, _api.Cancellations);
    }

    [Fact]
    public void ApiValidationErrorsAreRenderedAsTextWithSupportReference()
    {
        var page = Render<ApiError>(parameters => parameters.Add(component => component.Error,
            new AppointmentApiException(400, "Check your details.", "support-123",
                new Dictionary<string, string[]> { ["reason"] = ["<script>unsafe</script>"] })));

        Assert.Empty(page.FindAll("script"));
        Assert.Contains("<script>unsafe</script>", page.Find("li").TextContent);
        Assert.Contains("support-123", page.Markup);
    }

    [Fact]
    public void ExpiredSessionProvidesSignInAction()
    {
        var page = Render<ApiError>(parameters => parameters.Add(component => component.Error,
            new AppointmentApiException(401, "Your session has expired.")));

        Assert.Equal("Sign in again", page.Find("a[href='login']").TextContent);
    }

    [Fact]
    public void DisposingAvailabilityCancelsPendingWork()
    {
        _api.PendingSearch = new TaskCompletionSource<AvailableSlotsPage>();
        var page = Render<AvailabilityPanel>();
        page.Find("#clinic-reference").Change(FakeAppointmentApiClient.ClinicId.ToString());
        page.Find("form").Submit();

        page.Dispose();

        Assert.Equal(1, _api.Searches);
    }

    private static void Search(IRenderedComponent<AvailabilityPanel> page)
    {
        page.Find("#clinic-reference").Change(FakeAppointmentApiClient.ClinicId.ToString());
        page.Find("form").Submit();
    }
}

