using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminClinicUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                table: "StaffInvitations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReceptionistDoctorAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceptionistUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionistDoctorAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionistDoctorAssignments_ClinicId_ReceptionistUserId_DoctorId",
                table: "ReceptionistDoctorAssignments",
                columns: new[] { "ClinicId", "ReceptionistUserId", "DoctorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceptionistDoctorAssignments_ReceptionistUserId_ClinicId_IsActive",
                table: "ReceptionistDoctorAssignments",
                columns: new[] { "ReceptionistUserId", "ClinicId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceptionistDoctorAssignments");

            migrationBuilder.DropColumn(
                name: "PayloadJson",
                table: "StaffInvitations");
        }
    }
}
