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
            var result = await users.CreateAsync(new PortalUser
            {
                UserName = Email,
                Email = Email,
                PatientId = patient.Id,
                PhoneNumber = patient.PhoneNumber
            }, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Development account could not be created: " +
                    string.Join("; ", result.Errors.Select(x => x.Description)));
            }
            await transaction.CommitAsync();
        });

        (string Name, string Address, string Phone)[] demoClinics =
        [
            ("Central Community Clinic", "123 Main Street, Durban, 4001", "031 123 4567"),
            ("Westville Clinic", "45 Westville Road, Westville, 3629", "031 987 6543"),
            ("Phoenix Clinic", "78 Phoenix Highway, Phoenix, 4068", "031 555 1234"),
            ("Umlazi Clinic", "95 King Cetshwayo Road, Umlazi, 4031", "031 666 9876")
        ];
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(2)).DateTime);
        foreach (var demo in demoClinics)
        {
            var clinic = await context.Clinics.SingleOrDefaultAsync(x => x.Name == demo.Name);
            if (clinic is null)
            {
                clinic = new Clinic(demo.Name, demo.Address, demo.Phone);
                context.Clinics.Add(clinic);
                await context.SaveChangesAsync();
            }
            var existing = await context.TimeSlots.Where(x => x.ClinicId == clinic.Id && x.StartsAt > now)
                .Select(x => x.StartsAt).ToListAsync();
            var starts = existing.ToHashSet();
            for (var day = 0; day < 15; day++)
            {
                var date = today.AddDays(day);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    continue;
                }
                for (var hour = 8; hour < 17; hour++)
                {
                    for (var minute = 0; minute < 60; minute += 30)
                    {
                        var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(2));
                        if (start > now && starts.Add(start))
                        {
                            context.TimeSlots.Add(new TimeSlot(clinic, start, start.AddMinutes(30)));
                        }
                    }
                }
            }
            await context.SaveChangesAsync();
        }
        app.Logger.LogInformation("Development setup ready: four demo clinics and future weekday slots. Demo email: {Email}", Email);
    }
}

