using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentClinicLocalDateDuplicateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ClinicLocalDate",
                table: "Appointments",
                type: "date",
                nullable: true);

            // Backfill clinic-local calendar date (India Standard Time for ProDoc clinic ops).
            migrationBuilder.Sql("""
                UPDATE Appointments
                SET ClinicLocalDate = CAST((StartsAtUtc AT TIME ZONE 'UTC' AT TIME ZONE 'India Standard Time') AS date);
                """);

            // Free the unique day for expired holds that were never marked Expired.
            migrationBuilder.Sql("""
                UPDATE Appointments
                SET Status = 6, UpdatedAtUtc = SYSUTCDATETIME()
                WHERE Status = 1
                  AND HoldExpiresAtUtc IS NOT NULL
                  AND HoldExpiresAtUtc <= SYSUTCDATETIME();
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ClinicLocalDate",
                table: "Appointments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AccountUserId_DoctorId_ClinicLocalDate",
                table: "Appointments",
                columns: new[] { "AccountUserId", "DoctorId", "ClinicLocalDate" },
                unique: true,
                filter: "[Status] IN (1, 2, 3, 7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_AccountUserId_DoctorId_ClinicLocalDate",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClinicLocalDate",
                table: "Appointments");
        }
    }
}
