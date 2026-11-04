using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Identity;

namespace ProDoc.Infrastructure.Persistence.Configurations;

public sealed class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
{
    public void Configure(EntityTypeBuilder<Specialization> builder)
    {
        builder.ToTable("Specializations");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Code).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
    }
}

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");
        builder.Property(doctor => doctor.Id).ValueGeneratedNever();
        builder.Property(doctor => doctor.Reference).HasMaxLength(20).IsRequired();
        builder.Property(doctor => doctor.FirstName).HasMaxLength(40);
        builder.Property(doctor => doctor.LastName).HasMaxLength(40);
        builder.Property(doctor => doctor.Gender).HasMaxLength(20);
        builder.Property(doctor => doctor.Email).HasMaxLength(160);
        builder.Property(doctor => doctor.PreferredLanguage).HasMaxLength(8).IsRequired();
        builder.Property(doctor => doctor.DisplayName).HasMaxLength(120);
        builder.Property(doctor => doctor.Biography).HasMaxLength(2000);
        builder.Property(doctor => doctor.RejectionReason).HasMaxLength(500);
        builder.Property(doctor => doctor.RowVersion).IsRowVersion();
        builder.Property(doctor => doctor.Status).HasConversion<int>();
        builder.HasIndex(doctor => doctor.UserId).IsUnique();
        builder.HasIndex(doctor => doctor.Reference).IsUnique();
        builder.HasIndex(doctor => doctor.Status);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(doctor => doctor.Qualifications).WithOne().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(doctor => doctor.Specializations).WithOne().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(doctor => doctor.Languages).WithOne().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(doctor => doctor.Expertise).WithOne().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(doctor => doctor.Registrations).WithOne().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(doctor => doctor.Documents).WithOne().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(doctor => doctor.Qualifications).HasField("_qualifications").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(doctor => doctor.Specializations).HasField("_specializations").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(doctor => doctor.Languages).HasField("_languages").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(doctor => doctor.Expertise).HasField("_expertise").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(doctor => doctor.Registrations).HasField("_registrations").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(doctor => doctor.Documents).HasField("_documents").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class DoctorQualificationConfiguration : IEntityTypeConfiguration<DoctorQualification>
{
    public void Configure(EntityTypeBuilder<DoctorQualification> builder)
    {
        builder.ToTable("DoctorQualifications");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Institution).HasMaxLength(160);
    }
}

public sealed class DoctorSpecializationConfiguration : IEntityTypeConfiguration<DoctorSpecialization>
{
    public void Configure(EntityTypeBuilder<DoctorSpecialization> builder)
    {
        builder.ToTable("DoctorSpecializations");
        builder.HasKey(item => new { item.DoctorId, item.SpecializationId });
        builder.HasOne<Specialization>().WithMany().HasForeignKey(item => item.SpecializationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DoctorSpokenLanguageConfiguration : IEntityTypeConfiguration<DoctorSpokenLanguage>
{
    public void Configure(EntityTypeBuilder<DoctorSpokenLanguage> builder)
    {
        builder.ToTable("DoctorSpokenLanguages");
        builder.HasKey(item => new { item.DoctorId, item.Code });
        builder.Property(item => item.Code).HasMaxLength(8);
    }
}

public sealed class DoctorExpertiseConfiguration : IEntityTypeConfiguration<DoctorExpertise>
{
    public void Configure(EntityTypeBuilder<DoctorExpertise> builder)
    {
        builder.ToTable("DoctorExpertise");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Label).HasMaxLength(80).IsRequired();
    }
}

public sealed class DoctorRegistrationConfiguration : IEntityTypeConfiguration<DoctorRegistration>
{
    public void Configure(EntityTypeBuilder<DoctorRegistration> builder)
    {
        builder.ToTable("DoctorRegistrations");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Number).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Council).HasMaxLength(120).IsRequired();
        builder.Property(item => item.State).HasMaxLength(80).IsRequired();
        builder.Property(item => item.RegistrationType).HasMaxLength(20).IsRequired();
        builder.HasIndex(item => new { item.DoctorId, item.IsCurrent });
    }
}

public sealed class ProviderDocumentConfiguration : IEntityTypeConfiguration<ProviderDocument>
{
    public void Configure(EntityTypeBuilder<ProviderDocument> builder)
    {
        builder.ToTable("ProviderDocuments");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OriginalFileName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.StorageKey).HasMaxLength(80).IsRequired();
        builder.Property(item => item.ScanEngine).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ScanDetail).HasMaxLength(300).IsRequired();
        builder.Property(item => item.RejectionReason).HasMaxLength(500);
        builder.Property(item => item.Category).HasConversion<int>();
        builder.HasIndex(item => item.StorageKey).IsUnique();
        builder.HasIndex(item => new { item.DoctorId, item.Category, item.IsCurrent });
        builder.HasIndex(item => new { item.ClinicId, item.Category, item.IsCurrent });
    }
}

public sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.ToTable("Clinics");
        builder.Property(clinic => clinic.Id).ValueGeneratedNever();
        builder.Property(clinic => clinic.Reference).HasMaxLength(20).IsRequired();
        builder.Property(clinic => clinic.Name).HasMaxLength(160);
        builder.Property(clinic => clinic.LegalName).HasMaxLength(200);
        builder.Property(clinic => clinic.Description).HasMaxLength(2000);
        builder.Property(clinic => clinic.PhoneNumber).HasMaxLength(16);
        builder.Property(clinic => clinic.Email).HasMaxLength(160);
        builder.Property(clinic => clinic.Website).HasMaxLength(200);
        builder.Property(clinic => clinic.RegistrationNumber).HasMaxLength(40);
        builder.Property(clinic => clinic.RegistrationAuthority).HasMaxLength(120);
        builder.Property(clinic => clinic.RegistrationState).HasMaxLength(80);
        builder.Property(clinic => clinic.RejectionReason).HasMaxLength(500);
        builder.Property(clinic => clinic.RowVersion).IsRowVersion();
        builder.Property(clinic => clinic.Status).HasConversion<int>();
        builder.Property(clinic => clinic.Type).HasConversion<int>();
        builder.HasIndex(clinic => clinic.Reference).IsUnique();
        builder.HasIndex(clinic => clinic.OwnerUserId);
        builder.HasIndex(clinic => clinic.OrganizationId);
        builder.HasIndex(clinic => clinic.Status);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(clinic => clinic.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(clinic => clinic.Branches).WithOne().HasForeignKey(branch => branch.ClinicId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(clinic => clinic.Documents).WithOne().HasForeignKey(document => document.ClinicId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(clinic => clinic.Branches).HasField("_branches").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(clinic => clinic.Documents).HasField("_documents").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ClinicBranchConfiguration : IEntityTypeConfiguration<ClinicBranch>
{
    public void Configure(EntityTypeBuilder<ClinicBranch> builder)
    {
        builder.ToTable("ClinicBranches");
        builder.Property(branch => branch.Id).ValueGeneratedNever();
        builder.Property(branch => branch.Name).HasMaxLength(120).IsRequired();
        builder.Property(branch => branch.AddressLine).HasMaxLength(200).IsRequired();
        builder.Property(branch => branch.AddressLine2).HasMaxLength(200);
        builder.Property(branch => branch.City).HasMaxLength(80).IsRequired();
        builder.Property(branch => branch.State).HasMaxLength(80).IsRequired();
        builder.Property(branch => branch.PostalCode).HasMaxLength(6).IsRequired();
        builder.HasIndex(branch => branch.City);
        builder.HasIndex(branch => branch.PostalCode);
        builder.Property(branch => branch.PhoneNumber).HasMaxLength(16);
        builder.Property(branch => branch.Latitude).HasPrecision(9, 6);
        builder.Property(branch => branch.Longitude).HasPrecision(9, 6);
    }
}

public sealed class DoctorClinicAffiliationConfiguration : IEntityTypeConfiguration<DoctorClinicAffiliation>
{
    public void Configure(EntityTypeBuilder<DoctorClinicAffiliation> builder)
    {
        builder.ToTable("DoctorClinicAffiliations");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.HasIndex(item => new { item.DoctorId, item.ClinicId }).IsUnique();
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Clinic>().WithMany().HasForeignKey(item => item.ClinicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicBranch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
