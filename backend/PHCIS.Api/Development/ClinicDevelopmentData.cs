using Microsoft.EntityFrameworkCore;
using PHCIS.Domain.Entities;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api.Development;

/// <summary>Adds fictional clinics and future slots without creating accounts or changing bookings.</summary>
public static class ClinicDevelopmentData
{
    /// <summary>Seeds the configured database only when enabled in the Development environment.</summary>
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Development:SeedClinicsAndSlots"))
        {
            return;
        }
        await using var scope = app.Services.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<PhcisDbContext>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>());
        app.Logger.LogInformation("Development clinics and future weekday slots are ready.");
    }

    /// <summary>Adds missing demo clinics and 30-minute weekday slots over the next 15 calendar days.</summary>
    public static async Task SeedAsync(PhcisDbContext context, TimeProvider clock)
    {
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
            var clinic = await context.Clinics.SingleOrDefaultAsync(x => x.DevelopmentSeedKey == demo.Name ||
                (x.DevelopmentSeedKey == null && x.Name == demo.Name));
            if (clinic is null)
            {
                clinic = new Clinic(demo.Name, demo.Address, demo.Phone);
                context.Clinics.Add(clinic);
            }
            clinic.AssignDevelopmentSeedKey(demo.Name);
            await context.SaveChangesAsync();
            if (clinic.HasManagedSchedule)
                continue;
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
    }
}
