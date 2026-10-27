using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PHCIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Appointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Appointments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Cancellation",
                table: "Appointments",
                sql: "([Status] = 1 AND [CancelledAt] IS NOT NULL AND [CancelledAt] >= [CreatedAt] AND [CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0) OR ([Status] <> 1 AND [CancelledAt] IS NULL AND [CancellationReason] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Cancellation",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Appointments");
        }
    }
}
