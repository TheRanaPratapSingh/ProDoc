using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowAutomationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationFollowUpTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationFollowUpTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationOutboxEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeadLetterReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationOutboxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    FailureDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationWorkflowRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ConditionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ActionsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    DelayMinutes = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PushEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MarketingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AppointmentRemindersEnabled = table.Column<bool>(type: "bit", nullable: false),
                    QueueUpdatesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationFollowUpTasks_OrganizationId",
                table: "AutomationFollowUpTasks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationFollowUpTasks_Status_DueAtUtc",
                table: "AutomationFollowUpTasks",
                columns: new[] { "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationOutboxEvents_IdempotencyKey",
                table: "AutomationOutboxEvents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationOutboxEvents_Status_AvailableAtUtc",
                table: "AutomationOutboxEvents",
                columns: new[] { "Status", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationOutboxEvents_SubjectId",
                table: "AutomationOutboxEvents",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkflowRuns_OutboxEventId",
                table: "AutomationWorkflowRuns",
                column: "OutboxEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkflowRuns_WorkflowId_StartedAtUtc",
                table: "AutomationWorkflowRuns",
                columns: new[] { "WorkflowId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkflows_OrganizationId",
                table: "AutomationWorkflows",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationWorkflows_TriggerType_IsEnabled",
                table: "AutomationWorkflows",
                columns: new[] { "TriggerType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_UserId",
                table: "UserNotificationPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationFollowUpTasks");

            migrationBuilder.DropTable(
                name: "AutomationOutboxEvents");

            migrationBuilder.DropTable(
                name: "AutomationWorkflowRuns");

            migrationBuilder.DropTable(
                name: "AutomationWorkflows");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");
        }
    }
}
