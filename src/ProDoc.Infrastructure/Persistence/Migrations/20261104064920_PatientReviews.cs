using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetKind = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverallRating = table.Column<int>(type: "int", nullable: false),
                    CommunicationRating = table.Column<int>(type: "int", nullable: true),
                    CleanlinessRating = table.Column<int>(type: "int", nullable: true),
                    StaffBehaviourRating = table.Column<int>(type: "int", nullable: true),
                    WaitingTimeRating = table.Column<int>(type: "int", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsVerifiedVisit = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewModerationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModeratorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewModerationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewModerationEvents_PatientReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PatientReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewReports_PatientReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PatientReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponderKind = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewResponses_PatientReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PatientReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSubmissions_AuthorUserId_CreatedAtUtc",
                table: "FeedbackSubmissions",
                columns: new[] { "AuthorUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientReviews_AppointmentId_TargetKind",
                table: "PatientReviews",
                columns: new[] { "AppointmentId", "TargetKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientReviews_AuthorUserId",
                table: "PatientReviews",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientReviews_ClinicId_Status_CreatedAtUtc",
                table: "PatientReviews",
                columns: new[] { "ClinicId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientReviews_DoctorId_Status_CreatedAtUtc",
                table: "PatientReviews",
                columns: new[] { "DoctorId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewModerationEvents_ReviewId_CreatedAtUtc",
                table: "ReviewModerationEvents",
                columns: new[] { "ReviewId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ReviewId_ReporterUserId_ResolvedAtUtc",
                table: "ReviewReports",
                columns: new[] { "ReviewId", "ReporterUserId", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewResponses_ReviewId_ResponderKind",
                table: "ReviewResponses",
                columns: new[] { "ReviewId", "ResponderKind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackSubmissions");

            migrationBuilder.DropTable(
                name: "ReviewModerationEvents");

            migrationBuilder.DropTable(
                name: "ReviewReports");

            migrationBuilder.DropTable(
                name: "ReviewResponses");

            migrationBuilder.DropTable(
                name: "PatientReviews");
        }
    }
}
