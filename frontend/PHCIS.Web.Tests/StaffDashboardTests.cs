using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Components;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class StaffDashboardTests : BunitContext
{
    private readonly Guid _clinicId = Guid.NewGuid();
    private readonly Guid _appointmentId = Guid.NewGuid();

    [Fact]
    public void UsersWithoutStaffRoleCannotSeeSearchOrAppointments()
    {
        Configure(_ => throw new InvalidOperationException("Clinic data must not be requested."), allowed: false);
        var page = Render<StaffDashboard>();
        page.WaitForAssertion(() => Assert.Contains("Staff role is required", page.Markup));
        Assert.Empty(page.FindAll("form"));
        Assert.Empty(page.FindAll("table"));
    }

    [Theory]
    [InlineData(true, "completion", "Appointment marked completed.")]
    [InlineData(false, "no-show", "Appointment marked no-show.")]
    public void StaffConfirmsOutcomeAndReloadsAppointments(bool completed, string action, string message)
    {
        var finalized = false;
        Configure(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal($"/api/staff-appointments/{_clinicId}/{_appointmentId}/{action}", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("confirmStatusChange").GetBoolean());
                finalized = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return Results(finalized ? (completed ? "Completed" : "NoShow") : "Scheduled", !finalized);
        });
        var page = Render<StaffDashboard>();
        Search(page);
        var buttons = page.FindAll("tbody button");
        buttons[completed ? 0 : 1].Click();
        Assert.False(finalized);
        page.Find("#confirm-staff-outcome").Click();
        page.WaitForAssertion(() => Assert.Contains(message, page.Markup));
        Assert.True(finalized);
        Assert.All(page.FindAll("tbody button"), x => Assert.True(x.HasAttribute("disabled")));
    }

    [Fact]
    public void SearchValidatesClinicSelectionAndShowsEmptyState()
    {
        var reads = 0;
        Configure(request =>
        {
            reads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new StaffAppointmentsPage([], 0, 50, false))
            });
        });
        var page = Render<StaffDashboard>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("form").Submit();
        Assert.Equal(0, reads);
        Search(page, expectRow: false);
        page.WaitForAssertion(() => Assert.Contains("No appointments match", page.Markup));
    }

    [Fact]
    public void PrematureAppointmentsCannotBeFinalizedInTheUi()
    {
        Configure(_ => Task.FromResult(Results("Scheduled", false)));
        var page = Render<StaffDashboard>();
        Search(page);
        Assert.All(page.FindAll("tbody button"), x => Assert.True(x.HasAttribute("disabled")));
    }

    [Fact]
    public void ConflictKeepsCurrentStateAndShowsSafeError()
    {
        Configure(request => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { detail = "The appointment has already changed status." }) }
            : Results("Scheduled", true)));
        var page = Render<StaffDashboard>();
        Search(page);
        page.FindAll("tbody button")[0].Click();
        page.Find("#confirm-staff-outcome").Click();
        page.WaitForAssertion(() => Assert.Contains("already changed status", page.Markup));
        Assert.DoesNotContain("Appointment marked completed", page.Markup);
    }

    [Fact]
    public void StaffNavigationLinkAppearsOnlyForAllowedAccounts()
    {
        Configure(_ => throw new InvalidOperationException("No appointment query required."));
        var link = Render<StaffDashboardLink>();
        link.WaitForAssertion(() => Assert.Equal("/staff-dashboard", link.Find("a").GetAttribute("href")));
    }

    private void Search(IRenderedComponent<StaffDashboard> page, bool expectRow = true)
    {
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("#staff-clinic option").Count));
        page.Find("#staff-clinic").Change(_clinicId.ToString());
        page.Find("#staff-appointment-date").Change("2026-10-26");
        page.Find("#staff-appointments-search").Submit();
        if (expectRow)
            page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
    }

    private HttpResponseMessage Results(string status, bool canFinalize) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new StaffAppointmentsPage(new[]
        {
            new StaffAppointmentRow(_appointmentId, "Test Patient", new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 26, 8, 30, 0, TimeSpan.Zero), status, canFinalize)
        }, 0, 50, false))
    };

    private void Configure(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, bool allowed = true)
    {
        var http = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/staff-appointments/access")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new StaffDashboardAccess(allowed)) });
            if (request.RequestUri.AbsolutePath == "/api/clinics")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[] { new ClinicCard(_clinicId, "Test clinic", "Main Street", null) })
                });
            return respond(request);
        }))
        { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
    }
}
