using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiscoveryScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BufferMinutes",
                table: "ConsultationSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "ConsultationSchedules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveTo",
                table: "ConsultationSchedules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPatients",
                table: "ConsultationSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "ConsultationSchedules",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Kolkata");

            migrationBuilder.CreateTable(
                name: "BranchClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchClosures_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchOperatingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchOperatingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchOperatingHours_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicFacilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicFacilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicFacilities_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoctorFavourites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorFavourites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorFavourites_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleExtras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndsAt = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleExtras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleExtras_ConsultationSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ConsultationSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicBranches_City",
                table: "ClinicBranches",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicBranches_PostalCode",
                table: "ClinicBranches",
                column: "PostalCode");

            migrationBuilder.CreateIndex(
                name: "IX_BranchClosures_BranchId_Date",
                table: "BranchClosures",
                columns: new[] { "BranchId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchOperatingHours_BranchId",
                table: "BranchOperatingHours",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicFacilities_ClinicId_Name",
                table: "ClinicFacilities",
                columns: new[] { "ClinicId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorFavourites_DoctorId",
                table: "DoctorFavourites",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorFavourites_UserId_DoctorId",
                table: "DoctorFavourites",
                columns: new[] { "UserId", "DoctorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtras_ScheduleId",
                table: "ScheduleExtras",
                column: "ScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchClosures");

            migrationBuilder.DropTable(
                name: "BranchOperatingHours");

            migrationBuilder.DropTable(
                name: "ClinicFacilities");

            migrationBuilder.DropTable(
                name: "DoctorFavourites");

            migrationBuilder.DropTable(
                name: "ScheduleExtras");

            migrationBuilder.DropIndex(
                name: "IX_ClinicBranches_City",
                table: "ClinicBranches");

            migrationBuilder.DropIndex(
                name: "IX_ClinicBranches_PostalCode",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "BufferMinutes",
                table: "ConsultationSchedules");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "ConsultationSchedules");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "ConsultationSchedules");

            migrationBuilder.DropColumn(
                name: "MaxPatients",
                table: "ConsultationSchedules");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "ConsultationSchedules");
        }
    }
}
