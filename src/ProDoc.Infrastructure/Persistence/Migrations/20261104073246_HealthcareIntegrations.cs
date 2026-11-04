using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HealthcareIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbhaLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SandboxHandle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbhaLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKind = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScopeJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderKind = table.Column<int>(type: "int", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationDeliveryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CredentialReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKind = table.Column<int>(type: "int", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureValid = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CatalogCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TestName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ResultsArePreliminary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    IsPreliminary = table.Column<bool>(type: "bit", nullable: false),
                    Disclaimer = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PharmacyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExternalShareId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SharedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyShares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbhaLinks_UserId",
                table: "AbhaLinks",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConsents_UserId_ProviderKind_Purpose",
                table: "IntegrationConsents",
                columns: new[] { "UserId", "ProviderKind", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationDeliveryLogs_IdempotencyKey",
                table: "IntegrationDeliveryLogs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationDeliveryLogs_ProviderKind_CreatedAtUtc",
                table: "IntegrationDeliveryLogs",
                columns: new[] { "ProviderKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProviders_Code",
                table: "IntegrationProviders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationWebhookEvents_ProviderCode_EventId",
                table: "IntegrationWebhookEvents",
                columns: new[] { "ProviderCode", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_PatientUserId",
                table: "LabOrders",
                column: "PatientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_ProviderCode_ExternalOrderId",
                table: "LabOrders",
                columns: new[] { "ProviderCode", "ExternalOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_LabOrderId",
                table: "LabReports",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_PatientUserId",
                table: "LabReports",
                column: "PatientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyShares_PatientUserId",
                table: "PharmacyShares",
                column: "PatientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyShares_ProviderCode_ExternalShareId",
                table: "PharmacyShares",
                columns: new[] { "ProviderCode", "ExternalShareId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbhaLinks");

            migrationBuilder.DropTable(
                name: "IntegrationConsents");

            migrationBuilder.DropTable(
                name: "IntegrationDeliveryLogs");

            migrationBuilder.DropTable(
                name: "IntegrationProviders");

            migrationBuilder.DropTable(
                name: "IntegrationWebhookEvents");

            migrationBuilder.DropTable(
                name: "LabOrders");

            migrationBuilder.DropTable(
                name: "LabReports");

            migrationBuilder.DropTable(
                name: "PharmacyShares");
        }
    }
}
