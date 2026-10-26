using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class ClinicManagementUiTests : BunitContext
{
    private int _creates;

    private void Configure(bool allowed, HttpStatusCode createStatus = HttpStatusCode.Created)
    {
        var http = new HttpClient(new StubHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                _creates++;
                return Task.FromResult(new HttpResponseMessage(createStatus));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ClinicManagementAccess(allowed))
            });
        }))
        { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
    }

    [Fact]
    public void PatientsCannotSeeCreationForm()
    {
        Configure(false);
        var page = Render<ManageClinics>();
        page.WaitForAssertion(() => Assert.Contains("administrator access is required", page.Markup));
        Assert.Empty(page.FindAll("form"));
    }

    [Fact]
    public void InvalidFormDoesNotSendAndValidFormShowsSuccess()
    {
        Configure(true);
        var page = Render<ManageClinics>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("form").Submit();
        Assert.Equal(0, _creates);
        page.Find("#clinic-name").Change("New clinic");
        page.Find("#clinic-address").Change("10 Main Street");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("was added with initial availability", page.Markup));
        Assert.Equal(1, _creates);
        Assert.Equal("", page.Find("#clinic-name").GetAttribute("value"));
    }

    [Fact]
    public void FailedCreationKeepsEnteredDetailsAndShowsError()
    {
        Configure(true, HttpStatusCode.Conflict);
        var page = Render<ManageClinics>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("#clinic-name").Change("New clinic");
        page.Find("#clinic-address").Change("10 Main Street");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, _creates));
        Assert.Equal("New clinic", page.Find("#clinic-name").GetAttribute("value"));
        Assert.DoesNotContain("was added with initial availability", page.Markup);
    }
}
