using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Providers;

namespace ProDoc.Infrastructure.Clinical;

public sealed class PatientMedicalRecordConfiguration : IEntityTypeConfiguration<PatientMedicalRecord>
{
    public void Configure(EntityTypeBuilder<PatientMedicalRecord> builder)
    {
        builder.ToTable("PatientMedicalRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubjectName).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.AccountUserId, x.FamilyMemberId }).IsUnique();
        builder.HasMany(x => x.Allergies).WithOne().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Medications).WithOne().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Consultations).WithOne().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Prescriptions).WithOne().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Allergies).HasField("_allergies").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Conditions).HasField("_conditions").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Medications).HasField("_medications").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Documents).HasField("_documents").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Consultations).HasField("_consultations").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Prescriptions).HasField("_prescriptions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    public void Configure(EntityTypeBuilder<PatientAllergy> builder)
    {
        builder.ToTable("PatientAllergies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Substance).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Reaction).HasMaxLength(240);
        builder.Property(x => x.Severity).HasConversion<int>();
        builder.HasIndex(x => x.MedicalRecordId);
    }
}

public sealed class PatientMedicalConditionConfiguration : IEntityTypeConfiguration<PatientMedicalCondition>
{
    public void Configure(EntityTypeBuilder<PatientMedicalCondition> builder)
    {
        builder.ToTable("PatientMedicalConditions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.MedicalRecordId);
    }
}

public sealed class PatientMedicationConfiguration : IEntityTypeConfiguration<PatientMedication>
{
    public void Configure(EntityTypeBuilder<PatientMedication> builder)
    {
        builder.ToTable("PatientMedications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Dosage).HasMaxLength(80);
        builder.Property(x => x.Frequency).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.MedicalRecordId);
    }
}

public sealed class MedicalDocumentConfiguration : IEntityTypeConfiguration<MedicalDocument>
{
    public void Configure(EntityTypeBuilder<MedicalDocument> builder)
    {
        builder.ToTable("MedicalDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.MedicalRecordId);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}

public sealed class MedicineCatalogEntryConfiguration : IEntityTypeConfiguration<MedicineCatalogEntry>
{
    public void Configure(EntityTypeBuilder<MedicineCatalogEntry> builder)
    {
        builder.ToTable("MedicineCatalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Form).HasMaxLength(60);
        builder.Property(x => x.StrengthHint).HasMaxLength(60);
        builder.HasIndex(x => x.Name);
    }
}

public sealed class MedicalRecordAccessLogConfiguration : IEntityTypeConfiguration<MedicalRecordAccessLog>
{
    public void Configure(EntityTypeBuilder<MedicalRecordAccessLog> builder)
    {
        builder.ToTable("MedicalRecordAccessLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<int>();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(256);
        builder.HasIndex(x => new { x.MedicalRecordId, x.OccurredAtUtc });
        builder.HasIndex(x => x.ActorUserId);
    }
}

public sealed class ConsultationRecordConfiguration : IEntityTypeConfiguration<ConsultationRecord>
{
    public void Configure(EntityTypeBuilder<ConsultationRecord> builder)
    {
        builder.ToTable("ConsultationRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PatientName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Symptoms).HasMaxLength(2000);
        builder.Property(x => x.ClinicalObservations).HasMaxLength(4000);
        builder.Property(x => x.DoctorNotes).HasMaxLength(4000);
        builder.Property(x => x.Advice).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.MedicalRecordId);
        builder.HasIndex(x => x.DoctorId);
        builder.HasIndex(x => x.AppointmentId);
        builder.HasIndex(x => new { x.DoctorId, x.StartedAtUtc });
        builder.HasMany(x => x.Diagnoses).WithOne().HasForeignKey(x => x.ConsultationRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Diagnoses).HasField("_diagnoses").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ConsultationDiagnosisConfiguration : IEntityTypeConfiguration<ConsultationDiagnosis>
{
    public void Configure(EntityTypeBuilder<ConsultationDiagnosis> builder)
    {
        builder.ToTable("ConsultationDiagnoses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.HasIndex(x => x.ConsultationRecordId);
    }
}

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.MedicalRecordId);
        builder.HasIndex(x => x.DoctorId);
        builder.HasIndex(x => x.ConsultationRecordId);
        builder.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PrescriptionVersionConfiguration : IEntityTypeConfiguration<PrescriptionVersion>
{
    public void Configure(EntityTypeBuilder<PrescriptionVersion> builder)
    {
        builder.ToTable("PrescriptionVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Diagnosis).HasMaxLength(500);
        builder.Property(x => x.Symptoms).HasMaxLength(2000);
        builder.Property(x => x.ClinicalObservations).HasMaxLength(4000);
        builder.Property(x => x.Advice).HasMaxLength(2000);
        builder.Property(x => x.PatientNameSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DoctorNameSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DoctorRegistrationSnapshot).HasMaxLength(120);
        builder.Property(x => x.ClinicNameSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(x => x.BranchNameSnapshot).HasMaxLength(160).IsRequired();
        builder.Property(x => x.BranchAddressSnapshot).HasMaxLength(320);
        builder.Property(x => x.AmendmentReason).HasMaxLength(500);
        builder.Property(x => x.PdfStorageKey).HasMaxLength(64);
        builder.HasIndex(x => new { x.PrescriptionId, x.VersionNumber }).IsUnique();
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PrescriptionVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MedicineName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Dosage).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Frequency).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Duration).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Route).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Instructions).HasMaxLength(500);
        builder.HasIndex(x => x.PrescriptionVersionId);
    }
}

public sealed class PatientFollowUpConfiguration : IEntityTypeConfiguration<PatientFollowUp>
{
    public void Configure(EntityTypeBuilder<PatientFollowUp> builder)
    {
        builder.ToTable("PatientFollowUps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PatientName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FollowUpNote).HasMaxLength(500);
        builder.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => new { x.DoctorId, x.RecommendedDate });
        builder.HasIndex(x => new { x.PatientUserId, x.RecommendedDate });
        builder.HasIndex(x => new { x.ClinicId, x.RecommendedDate });
        builder.HasIndex(x => x.SourceConsultationId);
        builder.HasIndex(x => x.SourceAppointmentId);
        builder.HasIndex(x => x.LinkedAppointmentId);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Clinic>().WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
        // SQL Server rejects multiple cascade/SetNull paths from Appointments → PatientFollowUps.
        builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.SourceAppointmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.LinkedAppointmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ConsultationRecord>().WithMany().HasForeignKey(x => x.SourceConsultationId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class FollowUpReminderConfiguration : IEntityTypeConfiguration<FollowUpReminder>
{
    public void Configure(EntityTypeBuilder<FollowUpReminder> builder)
    {
        builder.ToTable("FollowUpReminders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(240);
        builder.Property(x => x.ReminderType).HasConversion<int>();
        builder.Property(x => x.DeliveryStatus).HasConversion<int>();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.FollowUpId, x.ScheduledAtUtc });
        builder.HasIndex(x => new { x.DeliveryStatus, x.ScheduledAtUtc });
        builder.HasOne<PatientFollowUp>().WithMany().HasForeignKey(x => x.FollowUpId).OnDelete(DeleteBehavior.Cascade);
    }
}
