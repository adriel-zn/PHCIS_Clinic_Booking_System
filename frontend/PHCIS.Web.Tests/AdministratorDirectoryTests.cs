using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class AdministratorDirectoryTests : BunitContext
{
    [Fact]
    public void LastAdministratorRemovalIsDisabled()
    {
        Configure(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { new AdministratorDetails("one", "admin@example.test", true, false) })
        }));
        var page = Render<Administrators>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.True(page.Find("tbody button").HasAttribute("disabled"));
        Assert.Contains("Last administrator", page.Markup);
    }

    [Fact]
    public void RemovalRequiresConfirmationThenRefreshesTheDirectory()
    {
        var removed = false;
        Configure(async request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/api/administrators/two/removal", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("confirmRemoval").GetBoolean());
                removed = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(removed
                    ? new[] { new AdministratorDetails("one", "admin@example.test", true, false) }
                    : new[] { new AdministratorDetails("one", "admin@example.test", true, true), new AdministratorDetails("two", "second@example.test", false, true) })
            };
        });
        var page = Render<Administrators>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        page.FindAll("tbody button")[1].Click();
        Assert.False(removed);
        page.Find("#confirm-admin-removal").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Contains("Administrator access removed", page.Markup);
    }

    [Fact]
    public void ServerRejectionIsShownAndTheAccountRemainsListed()
    {
        Configure(request => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { detail = "The last administrator cannot be removed." }) }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new AdministratorDetails("two", "second@example.test", false, true) })
            }));
        var page = Render<Administrators>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.Find("tbody button").Click();
        page.Find("#confirm-admin-removal").Click();
        page.WaitForAssertion(() => Assert.Contains("last administrator cannot be removed", page.Markup));
        Assert.Single(page.FindAll("tbody tr"));
    }

    private void Configure(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var http = new HttpClient(new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/access")
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ClinicManagementAccess(true)) })
            : respond(request)))
        { BaseAddress = new Uri("https://api.test/") };
        Services.AddSingleton(new PortalClient(http));
    }
}
