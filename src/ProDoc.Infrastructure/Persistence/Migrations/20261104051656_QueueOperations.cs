using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QueueOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc",
                table: "Appointments");

            migrationBuilder.CreateTable(
                name: "AppointmentCheckInTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentCheckInTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentCheckInTokens_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicQueueSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicQueueSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicQueueSessions_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicQueueSessions_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicQueueSessions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicQueueSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenPrefix = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ScheduledPatientPriority = table.Column<bool>(type: "bit", nullable: false),
                    EarlyCheckInMinutes = table.Column<int>(type: "int", nullable: false),
                    LateCheckInMinutes = table.Column<int>(type: "int", nullable: false),
                    AllowWalkIns = table.Column<bool>(type: "bit", nullable: false),
                    AllowPriorityPatients = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicQueueSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicQueueSettings_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicQueueSettings_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicStaffMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicStaffMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicStaffMembers_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicStaffMembers_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueueTokenCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastIssuedNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueTokenCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalkInRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientDisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReasonForVisit = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConsultationFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalkInRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalkInRecords_ClinicBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "ClinicBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalkInRecords_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalkInRecords_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueueEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WalkInRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TokenLabel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TokenNumber = table.Column<int>(type: "int", nullable: false),
                    QueuePosition = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckInMethod = table.Column<int>(type: "int", nullable: false),
                    IsWalkIn = table.Column<bool>(type: "bit", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    AppointmentStartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CheckInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CalledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsultationStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsultationCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueEntries_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueueEntries_ClinicQueueSessions_QueueSessionId",
                        column: x => x.QueueSessionId,
                        principalTable: "ClinicQueueSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueueEntries_WalkInRecords_WalkInRecordId",
                        column: x => x.WalkInRecordId,
                        principalTable: "WalkInRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueueStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueStatusHistory_QueueEntries_QueueEntryId",
                        column: x => x.QueueEntryId,
                        principalTable: "QueueEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "DoctorId", "BranchId", "StartsAtUtc" },
                unique: true,
                filter: "[Status] IN (1, 2, 3, 7, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckInTokens_AppointmentId",
                table: "AppointmentCheckInTokens",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentCheckInTokens_TokenHash",
                table: "AppointmentCheckInTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueSessions_BranchId",
                table: "ClinicQueueSessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueSessions_ClinicId_BranchId_DoctorId_OperatingDate",
                table: "ClinicQueueSessions",
                columns: new[] { "ClinicId", "BranchId", "DoctorId", "OperatingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueSessions_DoctorId",
                table: "ClinicQueueSessions",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueSettings_BranchId",
                table: "ClinicQueueSettings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueSettings_ClinicId_BranchId",
                table: "ClinicQueueSettings",
                columns: new[] { "ClinicId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicStaffMembers_BranchId",
                table: "ClinicStaffMembers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicStaffMembers_ClinicId_UserId",
                table: "ClinicStaffMembers",
                columns: new[] { "ClinicId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicStaffMembers_UserId",
                table: "ClinicStaffMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_AccountUserId",
                table: "QueueEntries",
                column: "AccountUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_AppointmentId",
                table: "QueueEntries",
                column: "AppointmentId",
                unique: true,
                filter: "[AppointmentId] IS NOT NULL AND [Status] IN (1, 2, 3, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_ClinicId_BranchId_DoctorId_OperatingDate_TokenNumber",
                table: "QueueEntries",
                columns: new[] { "ClinicId", "BranchId", "DoctorId", "OperatingDate", "TokenNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_DoctorId_BranchId_OperatingDate_Status",
                table: "QueueEntries",
                columns: new[] { "DoctorId", "BranchId", "OperatingDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_QueueSessionId",
                table: "QueueEntries",
                column: "QueueSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_WalkInRecordId",
                table: "QueueEntries",
                column: "WalkInRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueStatusHistory_QueueEntryId",
                table: "QueueStatusHistory",
                column: "QueueEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokenCounters_ClinicId_BranchId_DoctorId_OperatingDate",
                table: "QueueTokenCounters",
                columns: new[] { "ClinicId", "BranchId", "DoctorId", "OperatingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_Kind_SubjectId_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "Kind", "SubjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WalkInRecords_BranchId",
                table: "WalkInRecords",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WalkInRecords_ClinicId_BranchId_CreatedAtUtc",
                table: "WalkInRecords",
                columns: new[] { "ClinicId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WalkInRecords_DoctorId",
                table: "WalkInRecords",
                column: "DoctorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentCheckInTokens");

            migrationBuilder.DropTable(
                name: "ClinicQueueSettings");

            migrationBuilder.DropTable(
                name: "ClinicStaffMembers");

            migrationBuilder.DropTable(
                name: "QueueStatusHistory");

            migrationBuilder.DropTable(
                name: "QueueTokenCounters");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "QueueEntries");

            migrationBuilder.DropTable(
                name: "ClinicQueueSessions");

            migrationBuilder.DropTable(
                name: "WalkInRecords");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_BranchId_StartsAtUtc",
                table: "Appointments",
                columns: new[] { "DoctorId", "BranchId", "StartsAtUtc" },
                unique: true,
                filter: "[Status] IN (1, 2, 3)");
        }
    }
}
