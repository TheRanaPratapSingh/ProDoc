using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordsAccessAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ConsentVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordsConsentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PendingActionJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantUsageDaily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    PromptTokens = table.Column<int>(type: "int", nullable: false),
                    CompletionTokens = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantUsageDaily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ToolName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SourcesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantMessages_AssistantConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AssistantConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConsents_UserId",
                table: "AssistantConsents",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversations_DeletedAtUtc",
                table: "AssistantConversations",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversations_UserId_UpdatedAtUtc",
                table: "AssistantConversations",
                columns: new[] { "UserId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantMessages_ConversationId_CreatedAtUtc",
                table: "AssistantMessages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageDaily_UserId_UsageDate",
                table: "AssistantUsageDaily",
                columns: new[] { "UserId", "UsageDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantConsents");

            migrationBuilder.DropTable(
                name: "AssistantMessages");

            migrationBuilder.DropTable(
                name: "AssistantUsageDaily");

            migrationBuilder.DropTable(
                name: "AssistantConversations");
        }
    }
}
