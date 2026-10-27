using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Authentication;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Development;

/// <summary>Creates the explicitly enabled local administrator without changing existing credentials.</summary>
public static class AdministratorDevelopmentData
{
    /// <summary>Reserved development administrator login.</summary>
    public const string Email = "admin@phcis.local";
    /// <summary>Public local-development credential; never used outside the explicitly enabled Development seed.</summary>
    public const string FirstUsePassword = "AdminDemo!2026";

    /// <summary>Seeds only in Development, using the configured SQL Server and a local secret.</summary>
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Development:SeedAdministrator"))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await SeedAsync(context, users, scope.ServiceProvider.GetRequiredService<TimeProvider>(),
                app.Configuration["Development:AdministratorPassword"] ?? FirstUsePassword);
        });
        app.Logger.LogInformation("Development administrator seed completed; existing role memberships were preserved.");
    }

    /// <summary>Creates a patient-linked Identity administrator atomically; existing accounts are never promoted.</summary>
    public static async Task SeedAsync(PhcisDbContext context, UserManager<PortalUser> users,
        TimeProvider clock, string? password)
    {
        var existing = await users.FindByEmailAsync(Email);
        if (existing is not null)
        {
            // An existing account is never promoted again: a removed role must stay removed on restart.
            return;
        }
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set Development:AdministratorPassword in PHCIS.Api user secrets before administrator seeding.");

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync() : null;
        var patient = new Patient("Development", "Administrator", new DateOnly(1990, 1, 1), clock);
        patient.UpdateContactDetails(null, Email);
        context.Patients.Add(patient);
        var user = new PortalUser { UserName = Email, Email = Email, PatientId = patient.Id };
        EnsureSuccess(await users.CreateAsync(user, password));
        EnsureSuccess(await users.AddToRoleAsync(user, PortalRoles.Patient));
        EnsureSuccess(await users.AddToRoleAsync(user, PortalRoles.Admin));
        if (transaction is not null)
            await transaction.CommitAsync();
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Administrator seed failed: " + string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
