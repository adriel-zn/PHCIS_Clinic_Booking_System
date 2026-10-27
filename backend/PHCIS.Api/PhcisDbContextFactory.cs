using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PHCIS.Infrastructure.Persistence;

namespace PHCIS.Api;

/// <summary>Loads API configuration for EF commands without starting the web host.</summary>
public sealed class PhcisDbContextFactory : IDesignTimeDbContextFactory<PhcisDbContext>
{
    /// <summary>Creates a SQL Server context using the configured API connection string.</summary>
    public PhcisDbContext CreateDbContext(string[] args)
    {
        // Load the same settings and user secrets as the API, without starting it or seeding data.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development"
        });
        var connectionString = builder.Configuration.GetConnectionString("Phcis");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings:Phcis in API configuration before running EF migrations.");
        }
        return new PhcisDbContext(new DbContextOptionsBuilder<PhcisDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}

