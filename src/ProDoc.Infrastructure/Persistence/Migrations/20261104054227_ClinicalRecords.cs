using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicalRecordAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsultationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalRecordAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicineCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Form = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    StrengthHint = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedicalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedicalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QueueEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symptoms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClinicalObservations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DoctorNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Advice = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationRecords_PatientMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "PatientMedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalDocuments_PatientMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "PatientMedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Substance = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reaction = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    RecordedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAllergies_PatientMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "PatientMedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedicalConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OnsetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedicalConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientMedicalConditions_PatientMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "PatientMedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByDoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientMedications_PatientMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "PatientMedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsultationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescriptions_PatientMedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "PatientMedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationDiagnoses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsultationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationDiagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationDiagnoses_ConsultationRecords_ConsultationRecordId",
                        column: x => x.ConsultationRecordId,
                        principalTable: "ConsultationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Diagnosis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Symptoms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClinicalObservations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Advice = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PatientNameSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DoctorNameSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DoctorRegistrationSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ClinicNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    BranchNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    BranchAddressSnapshot = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ConsultationDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AmendedFromVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmendmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PdfStorageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionVersions_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrescriptionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicineCatalogEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MedicineName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Duration = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_PrescriptionVersions_PrescriptionVersionId",
                        column: x => x.PrescriptionVersionId,
                        principalTable: "PrescriptionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationDiagnoses_ConsultationRecordId",
                table: "ConsultationDiagnoses",
                column: "ConsultationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRecords_AppointmentId",
                table: "ConsultationRecords",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRecords_DoctorId",
                table: "ConsultationRecords",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRecords_DoctorId_StartedAtUtc",
                table: "ConsultationRecords",
                columns: new[] { "DoctorId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRecords_MedicalRecordId",
                table: "ConsultationRecords",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalDocuments_MedicalRecordId",
                table: "MedicalDocuments",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalDocuments_StorageKey",
                table: "MedicalDocuments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordAccessLogs_ActorUserId",
                table: "MedicalRecordAccessLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordAccessLogs_MedicalRecordId_OccurredAtUtc",
                table: "MedicalRecordAccessLogs",
                columns: new[] { "MedicalRecordId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineCatalog_Name",
                table: "MedicineCatalog",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAllergies_MedicalRecordId",
                table: "PatientAllergies",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicalConditions_MedicalRecordId",
                table: "PatientMedicalConditions",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicalRecords_AccountUserId_FamilyMemberId",
                table: "PatientMedicalRecords",
                columns: new[] { "AccountUserId", "FamilyMemberId" },
                unique: true,
                filter: "[FamilyMemberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedications_MedicalRecordId",
                table: "PatientMedications",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_PrescriptionVersionId",
                table: "PrescriptionItems",
                column: "PrescriptionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ConsultationRecordId",
                table: "Prescriptions",
                column: "ConsultationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_DoctorId",
                table: "Prescriptions",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_MedicalRecordId",
                table: "Prescriptions",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Number",
                table: "Prescriptions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionVersions_PrescriptionId_VersionNumber",
                table: "PrescriptionVersions",
                columns: new[] { "PrescriptionId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultationDiagnoses");

            migrationBuilder.DropTable(
                name: "MedicalDocuments");

            migrationBuilder.DropTable(
                name: "MedicalRecordAccessLogs");

            migrationBuilder.DropTable(
                name: "MedicineCatalog");

            migrationBuilder.DropTable(
                name: "PatientAllergies");

            migrationBuilder.DropTable(
                name: "PatientMedicalConditions");

            migrationBuilder.DropTable(
                name: "PatientMedications");

            migrationBuilder.DropTable(
                name: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "ConsultationRecords");

            migrationBuilder.DropTable(
                name: "PrescriptionVersions");

            migrationBuilder.DropTable(
                name: "Prescriptions");

            migrationBuilder.DropTable(
                name: "PatientMedicalRecords");
        }
    }
}
