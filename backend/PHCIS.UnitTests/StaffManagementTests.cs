using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class StaffManagementTests
{
    [Fact]
    public async Task AdministratorAssignsListsAndRemovesStaffWithoutChangingOtherRolesOrCredentials()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var admin = factory.Client(factory.Token());
        using var recipient = factory.Client();
        await RegisterAsync(recipient, "staff@example.test");
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var user = (await users.FindByEmailAsync("staff@example.test"))!;
        var passwordHash = user.PasswordHash;
        var patientId = user.PatientId;
        // Staff can coexist with Admin, and removal must not remove that independent membership.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/administrators",
            new { email = "staff@example.test", confirmAdministratorAccess = true })).StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/staff",
                new { email = "staff@example.test", confirmStaffAssignment = true })).StatusCode);
        var list = await ApiTests.Json(await admin.GetAsync("/api/staff"));
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(user.Id, list[0].GetProperty("id").GetString());
        Assert.Equal("staff@example.test", list[0].GetProperty("email").GetString());
        Assert.DoesNotContain("password", list.GetRawText(), StringComparison.OrdinalIgnoreCase);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/staff/{user.Id}/removal",
                new { confirmRemoval = true })).StatusCode);
        Assert.Equal(0, (await ApiTests.Json(await admin.GetAsync("/api/staff"))).GetArrayLength());
        using var fresh = factory.Services.CreateScope();
        var freshUsers = fresh.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var updated = (await freshUsers.FindByIdAsync(user.Id))!;
        Assert.True(await freshUsers.IsInRoleAsync(updated, PortalRoles.Patient));
        Assert.True(await freshUsers.IsInRoleAsync(updated, PortalRoles.Admin));
        Assert.False(await freshUsers.IsInRoleAsync(updated, PortalRoles.Staff));
        Assert.Equal(passwordHash, updated.PasswordHash);
        Assert.Equal(patientId, updated.PatientId);
        Assert.Equal(HttpStatusCode.OK, (await recipient.PostAsJsonAsync("/api/account/login",
            new { email = "staff@example.test", password = "StrongDemo!2026", rememberMe = false })).StatusCode);
    }

    [Fact]
    public async Task UnregisteredEmailsAndUnknownAccountsAreRejected()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/staff",
            new { email = "missing@example.test", confirmStaffAssignment = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/staff/missing/removal",
            new { confirmRemoval = true })).StatusCode);
    }

    [Fact]
    public async Task StaffOperationsValidateEmailAndConfirmation()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/staff",
            new { email = "invalid", confirmStaffAssignment = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/staff",
            new { email = "staff@example.test", confirmStaffAssignment = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/staff/any/removal",
            new { confirmRemoval = false })).StatusCode);
    }

    [Fact]
    public async Task AnonymousRequestsAreUnauthorized()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/staff",
            new { email = "staff@example.test", confirmStaffAssignment = true })).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeitherPatientsNorStaffCanManageStaff(bool assignStaff)
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        await RegisterAsync(client, "user@example.test");
        if (assignStaff)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("user@example.test"))!, PortalRoles.Staff)).Succeeded);
        }
        await client.PostAsJsonAsync("/api/account/login", new { email = "user@example.test", password = "StrongDemo!2026", rememberMe = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/staff",
            new { email = "user@example.test", confirmStaffAssignment = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/staff/any/removal",
            new { confirmRemoval = true })).StatusCode);
    }

    [Fact]
    public async Task StaffWritesRequireCsrfForAdministrators()
    {
        await using var factory = new ApiFactory(clinicAdministrator: true);
        var token = factory.Token();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PHCIS=" + token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/staff",
            new { email = "user@example.test", confirmStaffAssignment = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/staff/any/removal", new { confirmRemoval = true })).StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", new
        {
            firstName = "Test",
            lastName = "Staff",
            email,
            password = "StrongDemo!2026",
            phoneNumber = "082 555 0100",
            dateOfBirth = "1990-01-01"
        })).StatusCode);
}
