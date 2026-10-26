using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PHCIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicScheduleManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "TimeSlots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SchedulingVersion",
                table: "TimeSlots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DevelopmentSeedKey",
                table: "Clinics",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasManagedSchedule",
                table: "Clinics",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "TimeSlots");

            migrationBuilder.DropColumn(
                name: "SchedulingVersion",
                table: "TimeSlots");

            migrationBuilder.DropColumn(
                name: "DevelopmentSeedKey",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "HasManagedSchedule",
                table: "Clinics");
        }
    }
}
