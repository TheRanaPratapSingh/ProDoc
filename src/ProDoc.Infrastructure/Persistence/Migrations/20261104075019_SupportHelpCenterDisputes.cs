using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportHelpCenterDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "SupportTickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAtUtc",
                table: "SupportTickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAtUtc",
                table: "SupportTickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionDetails",
                table: "SupportTickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisputeCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceRequest = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecisionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LinkedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpArticleCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameHi = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticleCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    TitleHi = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    BodyHi = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketStatusHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedToUserId_Status",
                table: "SupportTickets",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_DueAtUtc",
                table: "SupportTickets",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeCases_Number",
                table: "DisputeCases",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisputeCases_Status_CreatedAtUtc",
                table: "DisputeCases",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeCases_SupportTicketId",
                table: "DisputeCases",
                column: "SupportTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticleCategories_Code",
                table: "HelpArticleCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticles_CategoryId_IsPublished",
                table: "HelpArticles",
                columns: new[] { "CategoryId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticles_Slug",
                table: "HelpArticles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportAttachments_StorageKey",
                table: "SupportAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportAttachments_SupportTicketId",
                table: "SupportAttachments",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistory_SupportTicketId",
                table: "TicketStatusHistory",
                column: "SupportTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisputeCases");

            migrationBuilder.DropTable(
                name: "HelpArticleCategories");

            migrationBuilder.DropTable(
                name: "HelpArticles");

            migrationBuilder.DropTable(
                name: "SupportAttachments");

            migrationBuilder.DropTable(
                name: "TicketStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_AssignedToUserId_Status",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_DueAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "DueAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "EscalatedAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ResolutionDetails",
                table: "SupportTickets");
        }
    }
}
