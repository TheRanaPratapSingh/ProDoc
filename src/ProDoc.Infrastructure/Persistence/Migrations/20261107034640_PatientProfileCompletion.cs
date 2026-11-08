using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientProfileCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgeYears",
                table: "Patients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Patients",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProfileCompleted",
                table: "Patients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Grandfather existing named patients so they are not blocked after deploy.
            migrationBuilder.Sql(
                """
                UPDATE Patients
                SET ProfileCompleted = 1
                WHERE FullName IS NOT NULL AND LTRIM(RTRIM(FullName)) <> N''
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ProfileCompleted",
                table: "Patients",
                column: "ProfileCompleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_ProfileCompleted",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AgeYears",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ProfileCompleted",
                table: "Patients");
        }
    }
}
