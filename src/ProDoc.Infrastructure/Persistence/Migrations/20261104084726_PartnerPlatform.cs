using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnerPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerApiUsageDaily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApiUsageDaily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerApps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScopesCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: false),
                    DailyQuota = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsSandbox = table.Column<bool>(type: "bit", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExternalPatientRef = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PatientDisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerBookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SecretPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerOrganizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Website = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerWebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerWebhookEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastStatusCode = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerWebhookDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerWebhookEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SigningSecret = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SecretPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EventTypesCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerWebhookEndpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiUsageDaily_PartnerAppId_Day",
                table: "PartnerApiUsageDaily",
                columns: new[] { "PartnerAppId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApps_ClientId",
                table: "PartnerApps",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApps_PartnerOrganizationId",
                table: "PartnerApps",
                column: "PartnerOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerBookings_PartnerAppId",
                table: "PartnerBookings",
                column: "PartnerAppId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerBookings_PartnerAppId_IdempotencyKey",
                table: "PartnerBookings",
                columns: new[] { "PartnerAppId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerCredentials_PartnerAppId",
                table: "PartnerCredentials",
                column: "PartnerAppId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerCredentials_SecretHash",
                table: "PartnerCredentials",
                column: "SecretHash");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerOrganizations_OwnerUserId",
                table: "PartnerOrganizations",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerOrganizations_Status",
                table: "PartnerOrganizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerWebhookDeliveries_IdempotencyKey",
                table: "PartnerWebhookDeliveries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerWebhookDeliveries_PartnerAppId",
                table: "PartnerWebhookDeliveries",
                column: "PartnerAppId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerWebhookDeliveries_Status_AvailableAtUtc",
                table: "PartnerWebhookDeliveries",
                columns: new[] { "Status", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerWebhookEndpoints_IsActive",
                table: "PartnerWebhookEndpoints",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerWebhookEndpoints_PartnerAppId",
                table: "PartnerWebhookEndpoints",
                column: "PartnerAppId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerApiUsageDaily");

            migrationBuilder.DropTable(
                name: "PartnerApps");

            migrationBuilder.DropTable(
                name: "PartnerBookings");

            migrationBuilder.DropTable(
                name: "PartnerCredentials");

            migrationBuilder.DropTable(
                name: "PartnerOrganizations");

            migrationBuilder.DropTable(
                name: "PartnerWebhookDeliveries");

            migrationBuilder.DropTable(
                name: "PartnerWebhookEndpoints");
        }
    }
}
