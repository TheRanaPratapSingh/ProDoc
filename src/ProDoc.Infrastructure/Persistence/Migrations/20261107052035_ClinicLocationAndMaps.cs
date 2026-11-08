using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicLocationAndMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PracticeAddressLine",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeAddressLine2",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeCity",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeCountry",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeDistrict",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeGooglePlaceId",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeLandmark",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PracticeLatitude",
                table: "DoctorClinicAffiliations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeLocality",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PracticeLocationUpdatedAtUtc",
                table: "DoctorClinicAffiliations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PracticeLongitude",
                table: "DoctorClinicAffiliations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticePostalCode",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeState",
                table: "DoctorClinicAffiliations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "ClinicBranches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "India");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "ClinicBranches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GooglePlaceId",
                table: "ClinicBranches",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Landmark",
                table: "ClinicBranches",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Locality",
                table: "ClinicBranches",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LocationUpdatedAtUtc",
                table: "ClinicBranches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationAddress",
                table: "Appointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationBranchName",
                table: "Appointments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationClinicName",
                table: "Appointments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationGooglePlaceId",
                table: "Appointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLatitude",
                table: "Appointments",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLongitude",
                table: "Appointments",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicBranches_Locality",
                table: "ClinicBranches",
                column: "Locality");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClinicBranches_Locality",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "PracticeAddressLine",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeAddressLine2",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeCity",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeCountry",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeDistrict",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeGooglePlaceId",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeLandmark",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeLatitude",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeLocality",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeLocationUpdatedAtUtc",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeLongitude",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticePostalCode",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "PracticeState",
                table: "DoctorClinicAffiliations");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "District",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "GooglePlaceId",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "Landmark",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "Locality",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "LocationUpdatedAtUtc",
                table: "ClinicBranches");

            migrationBuilder.DropColumn(
                name: "LocationAddress",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LocationBranchName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LocationClinicName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LocationGooglePlaceId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "Appointments");
        }
    }
}
