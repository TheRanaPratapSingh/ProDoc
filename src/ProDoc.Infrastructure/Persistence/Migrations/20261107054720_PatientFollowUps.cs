using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientFollowUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceConsultationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FollowUpNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresBooking = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LinkedAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientFollowUps_Appointments_LinkedAppointmentId",
                        column: x => x.LinkedAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientFollowUps_Appointments_SourceAppointmentId",
                        column: x => x.SourceAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientFollowUps_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientFollowUps_ConsultationRecords_SourceConsultationId",
                        column: x => x.SourceConsultationId,
                        principalTable: "ConsultationRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientFollowUps_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FollowUpReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowUpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderType = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUpReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowUpReminders_PatientFollowUps_FollowUpId",
                        column: x => x.FollowUpId,
                        principalTable: "PatientFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpReminders_DeliveryStatus_ScheduledAtUtc",
                table: "FollowUpReminders",
                columns: new[] { "DeliveryStatus", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpReminders_FollowUpId_ScheduledAtUtc",
                table: "FollowUpReminders",
                columns: new[] { "FollowUpId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpReminders_IdempotencyKey",
                table: "FollowUpReminders",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientFollowUps_ClinicId_RecommendedDate",
                table: "PatientFollowUps",
                columns: new[] { "ClinicId", "RecommendedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientFollowUps_DoctorId_RecommendedDate",
                table: "PatientFollowUps",
                columns: new[] { "DoctorId", "RecommendedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientFollowUps_LinkedAppointmentId",
                table: "PatientFollowUps",
                column: "LinkedAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientFollowUps_PatientUserId_RecommendedDate",
                table: "PatientFollowUps",
                columns: new[] { "PatientUserId", "RecommendedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientFollowUps_SourceAppointmentId",
                table: "PatientFollowUps",
                column: "SourceAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientFollowUps_SourceConsultationId",
                table: "PatientFollowUps",
                column: "SourceConsultationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowUpReminders");

            migrationBuilder.DropTable(
                name: "PatientFollowUps");
        }
    }
}
