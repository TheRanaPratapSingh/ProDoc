using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentVisitTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments");

            migrationBuilder.AddColumn<string>(
                name: "TokenLabel",
                table: "Appointments",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenNumber",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId_BranchId_DoctorId_TokenLabel_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "ClinicId", "BranchId", "DoctorId", "TokenLabel", "StartsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicId_BranchId_DoctorId_TokenLabel_StartsAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TokenLabel",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TokenNumber",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId",
                table: "Appointments",
                column: "ClinicId");
        }
    }
}
