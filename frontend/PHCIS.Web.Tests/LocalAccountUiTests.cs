using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Web.Components;
using PHCIS.Web.Models;
using PHCIS.Web.Pages;
using PHCIS.Web.Services;

namespace PHCIS.Web.Tests;

public sealed class LocalAccountUiTests : BunitContext
{
    private int _loginCalls;
    private readonly List<string> _paths = [];
    private bool _signedIn;
    private LocalAuthenticationStateProvider Session { get; }

    public LocalAccountUiTests()
    {
        var http = new HttpClient(new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _paths.Add(path);
            if (path == "/api/account/login")
            {
                _loginCalls++;
                _signedIn = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
            if (path == "/api/account/me")
            {
                return Task.FromResult(new HttpResponseMessage(_signedIn ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
                {
                    Content = _signedIn ? JsonContent.Create(new PatientProfile(
                        Guid.NewGuid(), "Test", "Patient", "patient@example.test", "082 555 0100", new DateOnly(1990, 1, 1), null)) : null
                });
            }
            if (path == "/api/clinics")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new ClinicCard(Guid.NewGuid(), "Central Community Clinic", "Main Street", "031 555 0100"),
                        new ClinicCard(Guid.NewGuid(), "Westville Clinic", "Westville Road", "031 555 0101")
                    })
                });
            }
            if (path == "/api/clinic-management/access")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ClinicManagementAccess(false))
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }))
        { BaseAddress = new Uri("https://api.test/") };
        Session = new LocalAuthenticationStateProvider(http);
        Services.AddSingleton(Session);
        Services.AddSingleton(new AccountClient(http, Session));
        Services.AddSingleton(new PortalClient(http));
    }

    [Fact]
    public void LoginHidesPublicDemoCredentialsByDefault()
    {
        var page = Render<LoginPanel>();
        Assert.Empty(page.FindAll(".development-login"));
        Assert.DoesNotContain("AdminDemo!2026", page.Markup);
    }

    [Fact]
    public void LocalFirstTimeLoginExplainsAdministratorAndPatientAccess()
    {
        Services.AddSingleton(new PHCIS.Web.Configuration.PortalOptions { ShowDevelopmentLogin = true });
        var page = Render<LoginPanel>();
        var hint = page.Find(".development-login");
        Assert.Contains("admin@phcis.local", hint.TextContent);
        Assert.Contains("AdminDemo!2026", hint.TextContent);
        Assert.Contains("patient account", hint.TextContent);
        Assert.Equal("", page.Find("#password").GetAttribute("value"));
    }

    [Fact]
    public void LoginValidatesBeforeSendingPassword()
    {
        var page = Render<LoginPanel>();
        page.Find("form").Submit();
        Assert.Equal(0, _loginCalls);
        Assert.Contains("Email", page.Find(".validation-errors").TextContent);
    }

    [Fact]
    public async Task SuccessfulLoginRehydratesServerProfileAndClearsPasswordField()
    {
        var page = Render<LoginPanel>();
        page.Find("#email").Change("patient@example.test");
        page.Find("#password").Change("StrongDemo!2026");

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Equal(1, _loginCalls));
        Assert.True((await Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Equal("Test Patient", Session.Profile!.FullName);
        Assert.Contains("/api/account/me", _paths);
        Assert.Equal("", page.Find("#password").GetAttribute("value"));
    }

    [Fact]
    public void RegistrationUsesLoginSceneAndLinksBackToSignIn()
    {
        var page = Render<Register>();

        Assert.Single(page.FindAll(".login-scene .login-card.registration-card"));
        Assert.Empty(page.FindAll(".sidebar"));
        Assert.Equal("/login", page.Find(".register-link a").GetAttribute("href"));
        Assert.Contains("Register as a Patient", page.Find("#register-heading").TextContent);
    }

    [Fact]
    public void RegistrationRequiresMatchingPasswordsBeforeCreatingAccount()
    {
        var page = Render<Register>();
        page.Find("#first-name").Change("Test");
        page.Find("#last-name").Change("Patient");
        page.Find("#register-email").Change("patient@example.test");
        page.Find("#phone").Change("082 555 0100");
        page.Find("#date-of-birth").Change("1990-01-01");
        page.Find("#new-password").Change("StrongDemo!2026");
        page.Find("#confirm-password").Change("Mismatch!2026");

        page.Find("form").Submit();

        Assert.DoesNotContain("/api/account/register", _paths);
        Assert.Contains("Passwords must match", page.Markup);
    }

    [Fact]
    public void ClinicDirectoryFiltersRealApiResults()
    {
        var page = Render<Clinics>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".clinic-card").Count));

        page.Find(".clinic-search").Input("Westville");

        Assert.Single(page.FindAll(".clinic-card"));
        Assert.Contains("Westville Clinic", page.Find(".clinic-card").TextContent);
    }
}

