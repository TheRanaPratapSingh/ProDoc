using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportHelpCenterEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByRole",
                table: "SupportTickets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IssueType",
                table: "SupportTickets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "HelpArticleFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WasHelpful = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticleFeedback", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticleFeedback_ArticleId",
                table: "HelpArticleFeedback",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticleFeedback_ArticleId_UserId",
                table: "HelpArticleFeedback",
                columns: new[] { "ArticleId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpArticleFeedback");

            migrationBuilder.DropColumn(
                name: "CreatedByRole",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "IssueType",
                table: "SupportTickets");
        }
    }
}
