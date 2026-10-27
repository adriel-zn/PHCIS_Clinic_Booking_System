using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Components;
using PHCIS.Web.Configuration;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class PlatformUiTests : BunitContext
{
    [Fact]
    public void StaffUserDirectoryOffersProfileEditingButNotCreationOrDeactivation()
    {
        var http = new HttpClient(new StubHandler(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("/access")
                ? JsonContent.Create(new UserManagementAccess(false, true))
                : JsonContent.Create(new ManagedUsersPage(new[] { new ManagedUser("patient", "patient@example.test", "Test", "Patient", "082 555 0100", new DateOnly(1990, 1, 1), true, false) }, false, 0, 50))
        })))
        { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
        var page = Render<Users>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Contains("Edit profile", page.Markup);
        Assert.DoesNotContain("Delete (archive)", page.Markup);
        Assert.DoesNotContain("Deactivate", page.Markup);
        Assert.Empty(page.FindAll("#managed-register-form"));
    }

    [Fact]
    public void StaffAssignmentPageLoadsTheSelectedUsersClinicsAndSavesNewScope()
    {
        var id = Guid.NewGuid();
        var assigned = false;
        var http = new HttpClient(new StubHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            { assigned = true; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            HttpContent body = request.RequestUri!.AbsolutePath == "/api/staff"
                ? JsonContent.Create(new[] { new StaffDetails("staff", "staff@example.test", false) })
                : JsonContent.Create(request.RequestUri.AbsolutePath == "/api/clinics" || assigned
                    ? new[] { new ClinicCard(id, "Assigned clinic", "Main Street", null) } : Array.Empty<ClinicCard>());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
        }))
        { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
        var page = Render<StaffClinicAssignments>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("#assignment-user option").Count));
        page.Find("#assignment-user").Change("staff");
        page.Find("#assignment-clinic").Change(id.ToString());
        page.FindAll("button").Single(b => b.TextContent == "Assign clinic").Click();
        page.WaitForAssertion(() => Assert.True(assigned));
        Assert.Contains("Assigned clinic", page.Markup);
    }

    [Fact]
    public async Task StaffDashboardUsesReadOnlyGraphQlWhenConfiguredAndLiveRefreshSignalIsConsumable()
    {
        string? query = null;
        var http = new HttpClient(new StubHandler(async request =>
        {
            Assert.Equal("/graphql", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            query = body.RootElement.GetProperty("query").GetString();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new { clinicAppointments = new StaffAppointmentsPage([], 0, 50, false) } }) };
        }))
        { BaseAddress = new Uri("https://api.test/") };
        var portal = new PortalClient(http, new PortalOptions { UseGraphQL = true });
        var calls = 0;
        portal.AppointmentsChanged += () => { calls++; return Task.CompletedTask; };
        await portal.ClinicAppointmentsAsync(Guid.NewGuid(), new DateOnly(2026, 10, 27), null, 0);
        await portal.NotifyChangedAsync();
        Assert.Contains("clinicAppointments", query);
        Assert.Equal(1, calls);
    }
}
