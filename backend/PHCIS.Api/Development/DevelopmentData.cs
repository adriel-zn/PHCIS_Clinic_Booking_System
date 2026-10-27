using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Identity;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Development;

/// <summary>Explicit, idempotent demo setup restricted to the dedicated development database.</summary>
public static class DevelopmentData
{
    /// <summary>Fictional account reserved for local demonstrations.</summary>
    public const string Email = "patient@phcis.local";

    /// <summary>Migrates and seeds only when explicitly enabled in Development.</summary>
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Development:InitializeDatabase"))
        {
            return;
        }
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        if (!context.Database.IsSqlServer())
        {
            throw new InvalidOperationException("Development initialization requires SQL Server LocalDB.");
        }
        var connection = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        if (connection.InitialCatalog != "PHCIS.Development" ||
            !connection.DataSource.Equals("(localdb)\\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Development initialization is restricted to MSSQLLocalDB / PHCIS.Development.");
        }
        await context.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            if (await users.FindByEmailAsync(Email) is not null)
            {
                return;
            }
            var password = app.Configuration["Development:SeedPassword"]
                ?? throw new InvalidOperationException("Set Development:SeedPassword in API user secrets before development initialization.");
            var patient = new Patient("Thando", "Mokoena", new DateOnly(1990, 1, 1), clock);
            patient.UpdateContactDetails("082 123 4567", Email);
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            var user = new PortalUser
            {
                UserName = Email,
                Email = Email,
                PatientId = patient.Id,
                PhoneNumber = patient.PhoneNumber
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Development account could not be created: " +
                    string.Join("; ", result.Errors.Select(x => x.Description)));
            }
            if (!(await users.AddToRoleAsync(user, PortalRoles.Patient)).Succeeded)
                throw new InvalidOperationException("Demo patient role could not be assigned.");
            await transaction.CommitAsync();
        });

        await ClinicDevelopmentData.SeedAsync(context, clock);
        app.Logger.LogInformation("Development setup ready: four demo clinics and future weekday slots. Demo email: {Email}", Email);
    }
}

