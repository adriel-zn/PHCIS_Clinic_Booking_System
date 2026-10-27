using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class StaffManagementUiTests : BunitContext
{
    [Fact]
    public void PatientsCannotViewStaffOrAssignmentForm()
    {
        Configure(_ => throw new InvalidOperationException("Directory must not be requested."), false);
        var page = Render<Staff>();
        page.WaitForAssertion(() => Assert.Contains("Administrator access is required", page.Markup));
        Assert.Empty(page.FindAll("form"));
        Assert.Empty(page.FindAll("table"));
    }

    [Fact]
    public void AssignmentValidatesAndRefreshesTheStaffDirectory()
    {
        var assigned = false;
        Configure(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/api/staff", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("confirmStaffAssignment").GetBoolean());
                assigned = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return Directory(assigned ? new[] { new StaffDetails("user", "staff@example.test", false) } : []);
        });
        var page = Render<Staff>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("No Staff members", page.Markup);
        page.Find("#staff-email").Change("staff@example.test");
        page.Find("form").Submit();
        Assert.False(assigned);
        page.Find("#confirm-staff-assignment").Change(true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.True(assigned);
        Assert.Contains("Staff role assigned", page.Markup);
    }

    [Fact]
    public void RemovalIsConfirmedBeforeSendingAndRefreshesEmptyList()
    {
        var removed = false;
        Configure(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/api/staff/user/removal", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("confirmRemoval").GetBoolean());
                removed = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return Directory(removed ? [] : new[] { new StaffDetails("user", "staff@example.test", false) });
        });
        var page = Render<Staff>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.Find("tbody button").Click();
        Assert.False(removed);
        page.Find("#confirm-staff-removal").Click();
        page.WaitForAssertion(() => Assert.Contains("Staff role removed", page.Markup));
        Assert.True(removed);
        Assert.Empty(page.FindAll("tbody tr"));
    }

    [Fact]
    public void FailedAssignmentShowsErrorAndPreservesEmail()
    {
        Configure(request => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { detail = "No registered account has this email." }) }
            : Directory([])));
        var page = Render<Staff>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("#staff-email").Change("missing@example.test");
        page.Find("#confirm-staff-assignment").Change(true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("No registered account", page.Markup));
        Assert.Equal("missing@example.test", page.Find("#staff-email").GetAttribute("value"));
        Assert.DoesNotContain("Staff role assigned", page.Markup);
    }

    private static HttpResponseMessage Directory(IReadOnlyList<StaffDetails> staff) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(staff) };

    private void Configure(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, bool allowed = true)
    {
        var http = new HttpClient(new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/access")
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ClinicManagementAccess(allowed)) })
            : respond(request)))
        { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
    }
}
