using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PHCIS.Api;
using PHCIS.Infrastructure;

namespace PHCIS.UnitTests;

public sealed class StartupMigrationTests
{
    [Fact]
    public async Task DisabledMigrationsDoNotResolveOrConnectToDatabase()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ApplyMigrationsOnStartup"] = "false"
        });
        await using var app = builder.Build();

        // No database services are registered: disabled migration startup must remain a no-op.
        await app.ApplyMigrationsAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingConnectionStringFailsWithConfigurationGuidance(string? connection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Phcis"] = connection
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("appsettings.json", error.Message);
    }
}
