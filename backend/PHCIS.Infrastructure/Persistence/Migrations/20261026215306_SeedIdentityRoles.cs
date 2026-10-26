using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PHCIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedIdentityRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve existing role IDs and memberships if roles were provisioned before this migration.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'PATIENT')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                    VALUES (N'phcis-patient-role', N'Patient', N'PATIENT', N'phcis-patient-role');
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'ADMIN')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                    VALUES (N'phcis-admin-role', N'Admin', N'ADMIN', N'phcis-admin-role');

                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT u.[Id], r.[Id] FROM [AspNetUsers] u
                CROSS JOIN [AspNetRoles] r
                WHERE r.[NormalizedName] = N'PATIENT'
                  AND NOT EXISTS (SELECT 1 FROM [AspNetUserRoles] ur WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]);

                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT DISTINCT c.[UserId], r.[Id] FROM [AspNetUserClaims] c
                CROSS JOIN [AspNetRoles] r
                WHERE c.[ClaimType] = N'phcis_permission' AND c.[ClaimValue] = N'manage_clinics'
                  AND r.[NormalizedName] = N'ADMIN'
                  AND NOT EXISTS (SELECT 1 FROM [AspNetUserRoles] ur WHERE ur.[UserId] = c.[UserId] AND ur.[RoleId] = r.[Id]);

                DELETE FROM [AspNetUserClaims] WHERE [ClaimType] = N'phcis_permission' AND [ClaimValue] = N'manage_clinics';
                """);
        }
            // Role assignments are security data. Keep them on rollback rather than deleting access.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
