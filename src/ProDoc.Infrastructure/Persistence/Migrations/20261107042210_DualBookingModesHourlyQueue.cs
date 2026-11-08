using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DualBookingModesHourlyQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTokenCounters_ClinicId_BranchId_DoctorId_OperatingDate",
                table: "QueueTokenCounters");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc",
                table: "Appointments");

            migrationBuilder.AddColumn<int>(
                name: "RangeStartMinutes",
                table: "QueueTokenCounters",
                type: "int",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<int>(
                name: "BookingMode",
                table: "ConsultationSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookingMode",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokenCounters_Clinic_Branch_Doctor_Date_Range",
                table: "QueueTokenCounters",
                columns: new[] { "ClinicId", "BranchId", "DoctorId", "OperatingDate", "RangeStartMinutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc_Timed",
                table: "Appointments",
                columns: new[] { "DoctorId", "BranchId", "StartsAtUtc" },
                unique: true,
                filter: "[Status] IN (1, 2, 3, 7, 8) AND [BookingMode] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_HourlyToken",
                table: "Appointments",
                columns: new[] { "DoctorId", "BranchId", "StartsAtUtc", "TokenLabel" },
                unique: true,
                filter: "[Status] IN (2, 3, 7, 8) AND [BookingMode] = 1 AND [TokenLabel] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTokenCounters_Clinic_Branch_Doctor_Date_Range",
                table: "QueueTokenCounters");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc_Timed",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_HourlyToken",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RangeStartMinutes",
                table: "QueueTokenCounters");

            migrationBuilder.DropColumn(
                name: "BookingMode",
                table: "ConsultationSchedules");

            migrationBuilder.DropColumn(
                name: "BookingMode",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokenCounters_ClinicId_BranchId_DoctorId_OperatingDate",
                table: "QueueTokenCounters",
                columns: new[] { "ClinicId", "BranchId", "DoctorId", "OperatingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "DoctorId", "BranchId", "StartsAtUtc" },
                unique: true,
                filter: "[Status] IN (1, 2, 3, 7, 8)");
        }
    }
}
