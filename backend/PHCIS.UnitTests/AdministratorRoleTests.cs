using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Api.Development;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class AdministratorRoleTests
{
    [Fact]
    public async Task SeededRolesAndNewRegistrationUseStandardIdentityTables()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        await RegisterAsync(client, "patient@example.test");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        Assert.Equal(new[] { "Admin", "Patient", "Staff" }, await context.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var user = await users.FindByEmailAsync("patient@example.test");
        Assert.Equal(new[] { PortalRoles.Patient }, await users.GetRolesAsync(user!));
        Assert.Empty(await context.UserClaims.ToListAsync());
    }

    [Fact]
    public async Task StaffRoleDoesNotGrantAdministratorPrivileges()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        await RegisterAsync(client, "staff@example.test");
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var user = (await users.FindByEmailAsync("staff@example.test"))!;
        Assert.True((await users.AddToRoleAsync(user, PortalRoles.Staff)).Succeeded);
        await LoginAsync(client, "staff@example.test");
        Assert.False((await ApiTests.Json(await client.GetAsync("/api/clinic-management/access"))).GetProperty("canManageClinics").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/administrators")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/administrators",
            new { email = "staff@example.test", confirmAdministratorAccess = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task LastAdministratorIsVisibleAndProtectedOnServer()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var admin = factory.Client(factory.Token());
        var list = await ApiTests.Json(await admin.GetAsync("/api/administrators"));
        Assert.Equal(1, list.GetArrayLength());
        Assert.False(list[0].GetProperty("canRemove").GetBoolean());
        Assert.True(list[0].GetProperty("isCurrentUser").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/administrators/test-subject/removal",
            new { confirmRemoval = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/administrators/test-subject/removal",
            new { confirmRemoval = false })).StatusCode);
    }

    [Fact]
    public async Task RemovedAdministratorLosesAccessWithExistingCookieButRemainsPatient()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var admin = factory.Client(factory.Token());
        using var target = factory.Client();
        await RegisterAsync(target, "second@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/administrators",
            new { email = "second@example.test", confirmAdministratorAccess = true })).StatusCode);
        await LoginAsync(target, "second@example.test");
        Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/api/administrators")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var user = (await users.FindByEmailAsync("second@example.test"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/administrators/{user.Id}/removal",
            new { confirmRemoval = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/administrators")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.PostAsJsonAsync("/api/administrators",
            new { email = "patient@example.test", confirmAdministratorAccess = true })).StatusCode);
        Assert.False((await ApiTests.Json(await target.GetAsync("/api/clinic-management/access"))).GetProperty("canManageClinics").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await target.GetAsync("/api/account/me")).StatusCode);
        using var freshScope = factory.Services.CreateScope();
        var freshUsers = freshScope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var updated = (await freshUsers.FindByIdAsync(user.Id))!;
        Assert.True(await freshUsers.IsInRoleAsync(updated, PortalRoles.Patient));
        Assert.False(await freshUsers.IsInRoleAsync(updated, PortalRoles.Admin));
    }

    [Fact]
    public async Task ConcurrentSelfRemovalsRetainOneAdministrator()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var first = factory.Client(factory.Token());
        using var second = factory.Client();
        await RegisterAsync(second, "second@example.test");
        await first.PostAsJsonAsync("/api/administrators", new { email = "second@example.test", confirmAdministratorAccess = true });
        await LoginAsync(second, "second@example.test");
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var target = (await users.FindByEmailAsync("second@example.test"))!;
        var results = await Task.WhenAll(
            first.PostAsJsonAsync("/api/administrators/test-subject/removal", new { confirmRemoval = true }),
            second.PostAsJsonAsync($"/api/administrators/{target.Id}/removal", new { confirmRemoval = true }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        using var fresh = factory.Services.CreateScope();
        Assert.Single(await fresh.ServiceProvider.GetRequiredService<UserManager<PortalUser>>().GetUsersInRoleAsync(PortalRoles.Admin));
    }

    [Fact]
    public async Task AdministratorSeedDoesNotRestoreRevokedMembership()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var admin = factory.Client(factory.Token());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        await AdministratorDevelopmentData.SeedAsync(context, users, new TestClock(DomainTestData.Now), "Temporary!2026");
        var seeded = (await users.FindByEmailAsync(AdministratorDevelopmentData.Email))!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/administrators/{seeded.Id}/removal",
            new { confirmRemoval = true })).StatusCode);
        using var fresh = factory.Services.CreateScope();
        var freshUsers = fresh.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        await AdministratorDevelopmentData.SeedAsync(fresh.ServiceProvider.GetRequiredService<PhcisDbContext>(), freshUsers,
            new TestClock(DomainTestData.Now), "Changed!2026");
        Assert.False(await freshUsers.IsInRoleAsync((await freshUsers.FindByIdAsync(seeded.Id))!, PortalRoles.Admin));
    }

    [Fact]
    public async Task OrdinaryPatientsCannotListOrRemoveAdministrators()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/administrators")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/administrators/anything/removal",
            new { confirmRemoval = true })).StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", new
        {
            firstName = "Test",
            lastName = "Account",
            email,
            password = "StrongDemo!2026",
            phoneNumber = "082 555 0100",
            dateOfBirth = "1990-01-01"
        })).StatusCode);

    private static async Task LoginAsync(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login", new
        {
            email,
            password = "StrongDemo!2026",
            rememberMe = false
        })).StatusCode);
}
