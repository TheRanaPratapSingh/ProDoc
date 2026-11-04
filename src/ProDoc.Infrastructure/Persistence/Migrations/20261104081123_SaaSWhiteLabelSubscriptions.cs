using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaaSWhiteLabelSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingInterval",
                table: "OrganizationSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "OrganizationSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                table: "OrganizationSubscriptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CurrentPeriodStartUtc",
                table: "OrganizationSubscriptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GracePeriodEndsUtc",
                table: "OrganizationSubscriptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPlanCode",
                table: "OrganizationSubscriptions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "OrganizationSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OrganizationSubscriptions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialEndsUtc",
                table: "OrganizationSubscriptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationBrandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogoStorageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PrimaryColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    AccentColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    LoginHeadline = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LoginSubheadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmailFromName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EmailFooterText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NotificationAccentHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    CustomDomain = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubdomainSlug = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBrandings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationFeatureOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LimitValue = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationFeatureOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaaSPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    RefundPolicyDays = table.Column<int>(type: "int", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaaSPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaaSSubscriptionCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    PlanCodeSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderOrderId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CheckoutPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaaSSubscriptionCharges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaaSPlanEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LimitValue = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaaSPlanEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaaSPlanEntitlements_SaaSPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SaaSPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_PlanId",
                table: "OrganizationSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_Status",
                table: "OrganizationSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBrandings_CustomDomain",
                table: "OrganizationBrandings",
                column: "CustomDomain",
                unique: true,
                filter: "[CustomDomain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBrandings_OrganizationId",
                table: "OrganizationBrandings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBrandings_SubdomainSlug",
                table: "OrganizationBrandings",
                column: "SubdomainSlug",
                unique: true,
                filter: "[SubdomainSlug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationFeatureOverrides_OrganizationId_FeatureCode",
                table: "OrganizationFeatureOverrides",
                columns: new[] { "OrganizationId", "FeatureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaaSPlanEntitlements_PlanId_FeatureCode",
                table: "SaaSPlanEntitlements",
                columns: new[] { "PlanId", "FeatureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaaSPlans_Code",
                table: "SaaSPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaaSPlans_IsActive_IsPublic_SortOrder",
                table: "SaaSPlans",
                columns: new[] { "IsActive", "IsPublic", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SaaSSubscriptionCharges_IdempotencyKey",
                table: "SaaSSubscriptionCharges",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaaSSubscriptionCharges_InvoiceId",
                table: "SaaSSubscriptionCharges",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SaaSSubscriptionCharges_OrganizationId_CreatedAtUtc",
                table: "SaaSSubscriptionCharges",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationBrandings");

            migrationBuilder.DropTable(
                name: "OrganizationFeatureOverrides");

            migrationBuilder.DropTable(
                name: "SaaSPlanEntitlements");

            migrationBuilder.DropTable(
                name: "SaaSSubscriptionCharges");

            migrationBuilder.DropTable(
                name: "SaaSPlans");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationSubscriptions_PlanId",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationSubscriptions_Status",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "BillingInterval",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStartUtc",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "GracePeriodEndsUtc",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "PendingPlanCode",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "TrialEndsUtc",
                table: "OrganizationSubscriptions");
        }
    }
}
