using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Scheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsultationSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsultationFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationSchedules_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultationSchedules_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultationSchedules_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleBreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndsAt = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBreaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleBreaks_ConsultationSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ConsultationSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleClosures_ConsultationSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ConsultationSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndsAt = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyHours_ConsultationSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ConsultationSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSchedules_BranchId",
                table: "ConsultationSchedules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSchedules_ClinicId",
                table: "ConsultationSchedules",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSchedules_DoctorId",
                table: "ConsultationSchedules",
                column: "DoctorId",
                unique: true,
                filter: "[ClinicId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSchedules_DoctorId_ClinicId_BranchId",
                table: "ConsultationSchedules",
                columns: new[] { "DoctorId", "ClinicId", "BranchId" },
                unique: true,
                filter: "[ClinicId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBreaks_ScheduleId",
                table: "ScheduleBreaks",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleClosures_ScheduleId_Date",
                table: "ScheduleClosures",
                columns: new[] { "ScheduleId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyHours_ScheduleId",
                table: "WeeklyHours",
                column: "ScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleBreaks");

            migrationBuilder.DropTable(
                name: "ScheduleClosures");

            migrationBuilder.DropTable(
                name: "WeeklyHours");

            migrationBuilder.DropTable(
                name: "ConsultationSchedules");
        }
    }
}
