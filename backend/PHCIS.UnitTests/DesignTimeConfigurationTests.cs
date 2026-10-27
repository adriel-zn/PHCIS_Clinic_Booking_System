using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using PHCIS.Api;

namespace PHCIS.UnitTests;

public sealed class DesignTimeConfigurationTests
{
    [Fact]
    public void MigrationsUseConfiguredSqlServerWithoutOpeningAConnection()
    {
        const string connection = "Server=example-server;Database=PHCIS.Test;Integrated Security=True;Encrypt=True";
        using var context = new PhcisDbContextFactory().CreateDbContext(
            ["--ConnectionStrings:Phcis", connection]);

        Assert.True(context.Database.IsSqlServer());
        var configured = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("example-server", configured.DataSource);
        Assert.Equal("PHCIS.Test", configured.InitialCatalog);
        Assert.True(configured.IntegratedSecurity);
    }
}
