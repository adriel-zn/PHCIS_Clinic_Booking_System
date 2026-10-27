using Microsoft.EntityFrameworkCore;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api;

/// <summary>Applies the checked-in SQL Server migrations before accepting API requests.</summary>
public static class DatabaseInitialization
{
    /// <summary>Updates the configured database when startup migrations are enabled.</summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PhcisDbContext>();
        // Do not use EnsureCreated: it bypasses EF migration history.
        app.Logger.LogInformation("Applying pending database migrations.");
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations completed.");
    }
}
