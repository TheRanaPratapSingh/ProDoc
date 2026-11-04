using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public sealed record RequestMetadata(string? IpAddress, string? UserAgent);

public sealed class DoctorOnboardingService(
    IProviderStore store,
    IClock clock,
    IVerificationNotifier notifier,
    ILogger<DoctorOnboardingService> logger)
{
    public async Task<DoctorProfile?> FindMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserIdAsync(userId, tracking: true, cancellationToken);
        if (doctor is null)
        {
            return null;
        }

        if (doctor.RefreshExpiry(Today, clock.UtcNow))
        {
            await store.SaveChangesAsync(cancellationToken);
        }

        return await MapAsync(doctor, userId, approvedClinicsOnly: false, cancellationToken);
    }

    public async Task<DoctorProfile> SaveBasicsAsync(
        Guid userId,
        string? firstName,
        string? lastName,
        DateOnly? dateOfBirth,
        string? gender,
        string? email,
        string preferredLanguage,
        CancellationToken cancellationToken)
    {
        var doctor = await EditableDoctorAsync(userId, preferredLanguage, cancellationToken);
        doctor.UpdateBasics(firstName, lastName, dateOfBirth, gender, email, preferredLanguage, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> SaveProfessionalAsync(
        Guid userId,
        string? displayName,
        string? biography,
        int? yearsOfExperience,
        IReadOnlyList<string>? languages,
        IReadOnlyList<string>? expertise,
        CancellationToken cancellationToken)
    {
        var doctor = await EditableDoctorAsync(userId, "en", cancellationToken);
        doctor.UpdateProfessional(displayName, biography, yearsOfExperience, languages, expertise, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> AddQualificationAsync(
        Guid userId,
        string name,
        string? institution,
        int graduationYear,
        CancellationToken cancellationToken)
    {
        var doctor = await EditableDoctorAsync(userId, "en", cancellationToken);
        doctor.AddQualification(name, institution, graduationYear, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> RemoveQualificationAsync(Guid userId, Guid qualificationId, CancellationToken cancellationToken)
    {
        var doctor = await EditableDoctorAsync(userId, "en", cancellationToken);
        doctor.RemoveQualification(qualificationId, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> SetSpecializationsAsync(
        Guid userId,
        IReadOnlyList<Guid> specializationIds,
        Guid primaryId,
        CancellationToken cancellationToken)
    {
        if (!await store.SpecializationsExistAsync(specializationIds.Append(primaryId).Distinct().ToArray(), cancellationToken))
        {
            throw DomainException.Validation("specialization_invalid", "Choose specializations from the catalog.");
        }

        var doctor = await EditableDoctorAsync(userId, "en", cancellationToken);
        doctor.SetSpecializations(specializationIds, primaryId, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> SaveRegistrationAsync(
        Guid userId,
        string number,
        string council,
        string state,
        string registrationType,
        DateOnly registeredOn,
        DateOnly? expiresOn,
        CancellationToken cancellationToken)
    {
        var doctor = await EditableDoctorAsync(userId, "en", cancellationToken);
        doctor.UpdateRegistration(number, council, state, registrationType, registeredOn, expiresOn, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> AttachDocumentAsync(
        Guid userId,
        DocumentCategory category,
        StoredUpload upload,
        CancellationToken cancellationToken)
    {
        var doctor = await EditableDoctorAsync(userId, "en", cancellationToken);
        doctor.AttachDocument(
            category,
            upload.FileName,
            upload.ContentType,
            upload.SizeBytes,
            upload.StorageKey,
            upload.ScanEngine,
            upload.ScanDetail,
            clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Stored doctor document {DocumentCategory} for doctor {DoctorId}", category, doctor.Id);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> SetAcceptingPatientsAsync(Guid userId, bool accepting, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserIdAsync(userId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "Start a doctor profile first.");
        doctor.SetAcceptingPatients(accepting, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> AffiliateAsync(
        Guid userId,
        Guid clinicId,
        Guid? branchId,
        AssociationKind kind,
        CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserIdAsync(userId, tracking: true, cancellationToken)
            ?? throw DomainException.Validation("doctor_not_found", "Start a doctor profile before linking a clinic.");
        var clinic = await store.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId != userId && !clinic.IsPubliclyListed(Today))
        {
            throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        }

        if (branchId is Guid selected && clinic.Branches.All(branch => branch.Id != selected || !branch.IsActive))
        {
            throw DomainException.Validation("branch_invalid", "Choose an active branch of that clinic.");
        }

        if (await store.FindAffiliationAsync(doctor.Id, clinicId, cancellationToken) is not null)
        {
            throw DomainException.Conflict("affiliation_exists", "This doctor is already linked to that clinic.");
        }

        var existing = await store.ListAffiliationsAsync(doctor.Id, cancellationToken);
        await store.AddAffiliationAsync(
            DoctorClinicAffiliation.Create(doctor.Id, clinicId, branchId, kind, existing.Count == 0, clock.UtcNow),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> RemoveAffiliationAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserIdAsync(userId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("doctor_not_found", "Start a doctor profile first.");
        var affiliation = await store.FindAffiliationAsync(doctor.Id, clinicId, cancellationToken)
            ?? throw DomainException.NotFound("affiliation_not_found", "That clinic link was not found.");
        var wasPrimary = affiliation.IsPrimary;
        store.RemoveAffiliation(affiliation);
        if (wasPrimary)
        {
            var remaining = await store.ListAffiliationsAsync(doctor.Id, cancellationToken);
            remaining.FirstOrDefault(item => item.Id != affiliation.Id)?.SetPrimary(true);
        }

        await store.SaveChangesAsync(cancellationToken);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    public async Task<DoctorProfile> SubmitAsync(Guid userId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserIdAsync(userId, tracking: true, cancellationToken)
            ?? throw DomainException.Validation("application_incomplete", "Start a doctor profile before submitting.");
        var now = clock.UtcNow;
        doctor.Submit(now);
        await store.AddAuditAsync(
            AuditLog.Record(userId, AuditActions.DoctorSubmitted, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        notifier.ApplicationSubmitted("doctor", doctor.Reference);
        logger.LogInformation("Doctor application {Reference} submitted", doctor.Reference);
        return await MapAsync(doctor, userId, false, cancellationToken);
    }

    private async Task<Doctor> EditableDoctorAsync(Guid userId, string preferredLanguage, CancellationToken cancellationToken)
    {
        var doctor = await store.FindDoctorByUserIdAsync(userId, tracking: true, cancellationToken);
        if (doctor is not null)
        {
            return doctor;
        }

        doctor = Doctor.Begin(userId, preferredLanguage, clock.UtcNow);
        await store.AddDoctorAsync(doctor, cancellationToken);
        return doctor;
    }

    private async Task<DoctorProfile> MapAsync(
        Doctor doctor,
        Guid userId,
        bool approvedClinicsOnly,
        CancellationToken cancellationToken)
    {
        var specializations = await store.ListSpecializationsAsync(cancellationToken);
        var affiliations = await store.ListAffiliationDetailsAsync(doctor.Id, approvedClinicsOnly, Today, cancellationToken);
        var phone = await store.FindUserPhoneAsync(userId, cancellationToken);
        return ProviderMapper.ToDoctor(
            doctor,
            phone,
            specializations.ToDictionary(item => item.Id),
            affiliations,
            Today);
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
}
