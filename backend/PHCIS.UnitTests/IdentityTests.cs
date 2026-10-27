using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class IdentityTests
{
    private static object Registration(string email = "new-patient@example.test", string password = "StrongDemo!2026") => new
    {
        firstName = "New",
        lastName = "Patient",
        email,
        phoneNumber = "082 555 0100",
        dateOfBirth = "1995-01-01",
        password
    };

    [Fact]
    public async Task RegisterLoginProfileBookCancelAndLogoutUseRealIdentityCookies()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", Registration())).StatusCode);

        var login = await client.PostAsJsonAsync("/api/account/login", new
        {
            email = "new-patient@example.test",
            password = "StrongDemo!2026",
            rememberMe = true
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = string.Join(";", login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await ApiTests.Json(me);
        Assert.Equal("new-patient@example.test", profile.GetProperty("email").GetString());
        var patientId = profile.GetProperty("id").GetGuid();

        var booked = await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var appointment = await ApiTests.Json(booked);
        Assert.Equal(patientId, appointment.GetProperty("patientId").GetGuid());
        var id = appointment.GetProperty("id").GetGuid();
        var history = await ApiTests.Json(await client.GetAsync("/api/appointments"));
        Assert.Single(history.EnumerateArray());

        var cancelled = await client.PostAsJsonAsync($"/api/appointments/{id}/cancellation", new { reason = "Testing cancellation" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await ApiTests.Json(cancelled)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var user = await users.FindByEmailAsync("new-patient@example.test");
        Assert.NotNull(user);
        Assert.NotEqual("StrongDemo!2026", user.PasswordHash);
        Assert.Equal(patientId, user.PatientId);
    }

    [Fact]
    public async Task FailedRegistrationDoesNotLeaveAnOrphanPatient()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/account/register", Registration(password: "weakpassword"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        Assert.Equal(0, await context.Patients.CountAsync());
        Assert.Equal(0, await context.Users.CountAsync());
    }

    [Fact]
    public async Task DuplicateRegistrationIsRejectedWithoutAddingPatient()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", Registration())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/register", Registration())).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        Assert.Equal(1, await context.Patients.CountAsync());
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task FiveFailedPasswordsLockAccountAndMessagesDoNotExposeAccountExistence()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        await client.PostAsJsonAsync("/api/account/register", Registration());
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/login",
                new { email = "new-patient@example.test", password = "WrongPassword!1" })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/login",
            new { email = "new-patient@example.test", password = "StrongDemo!2026" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByEmailAsync("new-patient@example.test"))!));
    }

    [Fact]
    public async Task UnsafeRequestWithoutCsrfIsRejectedEvenWithValidCookie()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PHCIS=" + factory.Token());
        var response = await client.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        var body = await ApiTests.AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Contains("Session", body.GetProperty("errors").GetRawText());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PhcisDbContext>().Appointments.CountAsync());
    }

    [Fact]
    public async Task LoginAndRegistrationRequireCsrfToo()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/register", Registration())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login",
            new { email = "new-patient@example.test", password = "StrongDemo!2026" })).StatusCode);
    }

    [Fact]
    public async Task AppointmentHistoryIsPatientScoped()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var owner = factory.Client(factory.Token());
        await owner.PostAsJsonAsync("/api/appointments", new { timeSlotId = factory.SlotId });
        using var other = factory.Client(factory.Token(Guid.NewGuid().ToString()));
        var own = await ApiTests.Json(await owner.GetAsync("/api/appointments"));
        var unrelated = await ApiTests.Json(await other.GetAsync("/api/appointments"));
        Assert.Single(own.EnumerateArray());
        Assert.Empty(unrelated.EnumerateArray());
    }
}

