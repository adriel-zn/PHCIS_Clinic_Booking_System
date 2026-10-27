using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PHCIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedStaffRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'STAFF')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                    VALUES (N'phcis-staff-role', N'Staff', N'STAFF', N'phcis-staff-role');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve the role and any assigned memberships when rolling back security seed data.
        }
    }
}
