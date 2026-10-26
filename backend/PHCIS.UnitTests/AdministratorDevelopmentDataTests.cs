using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Api.Development;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.UnitTests;

public sealed class AdministratorDevelopmentDataTests
{
    [Fact]
    public async Task SeededAdministratorCanLoginAndCreateClinicWithoutConfiguredAccountId()
    {
        await using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        await AdministratorDevelopmentData.SeedAsync(context, users, new TestClock(DomainTestData.Now), "AdminDemo!2026");
        await AdministratorDevelopmentData.SeedAsync(context, users, new TestClock(DomainTestData.Now), "Different!2026");
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.Patients.CountAsync());
        var user = await users.FindByEmailAsync(AdministratorDevelopmentData.Email);
        Assert.True(await users.CheckPasswordAsync(user!, "AdminDemo!2026"));
        Assert.NotEqual("AdminDemo!2026", user!.PasswordHash);

        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/login",
            new { email = AdministratorDevelopmentData.Email, password = "AdminDemo!2026", rememberMe = false })).StatusCode);
        var access = await ApiTests.Json(await client.GetAsync("/api/clinic-management/access"));
        Assert.True(access.GetProperty("canManageClinics").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/clinic-management",
            new { name = "Administrator clinic", address = "1 Main Street", phoneNumber = "031 555 0100" })).StatusCode);
    }

    [Fact]
    public async Task ExistingOrdinaryAccountIsNotPromotedOrReset()
    {
        await using var factory = new ApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/account/register", new
        {
            firstName = "Existing", lastName = "Patient", email = AdministratorDevelopmentData.Email,
            password = "Existing!2026", phoneNumber = "082 555 0100", dateOfBirth = "1990-01-01"
        })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => AdministratorDevelopmentData.SeedAsync(
            scope.ServiceProvider.GetRequiredService<PhcisDbContext>(), users,
            new TestClock(DomainTestData.Now), "AdminDemo!2026"));
        var user = await users.FindByEmailAsync(AdministratorDevelopmentData.Email);
        Assert.Empty(await users.GetClaimsAsync(user!));
        Assert.True(await users.CheckPasswordAsync(user!, "Existing!2026"));
    }

    [Theory]
    [InlineData("Production", "true")]
    [InlineData("Development", "false")]
    public async Task DisabledOrProductionSeedDoesNotResolveDatabase(string environment, string enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Development:SeedAdministrator"] = enabled
        });
        await using var app = builder.Build();
        await AdministratorDevelopmentData.InitializeAsync(app);
    }
}
