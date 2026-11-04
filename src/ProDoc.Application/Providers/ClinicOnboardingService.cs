using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Providers;

public sealed class ClinicOnboardingService(
    IProviderStore store,
    IClock clock,
    IVerificationNotifier notifier,
    ILogger<ClinicOnboardingService> logger)
{
    public async Task<IReadOnlyList<ClinicProfile>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var clinics = await store.ListClinicsByOwnerAsync(userId, cancellationToken);
        var today = Today;
        var changed = false;
        foreach (var clinic in clinics)
        {
            changed |= clinic.RefreshExpiry(today, clock.UtcNow);
        }

        if (changed)
        {
            await store.SaveChangesAsync(cancellationToken);
        }

        return clinics.Select(clinic => ProviderMapper.ToClinic(clinic, today)).ToArray();
    }

    public async Task<ClinicProfile> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var clinic = Clinic.Begin(userId, clock.UtcNow);
        await store.AddClinicAsync(clinic, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return ProviderMapper.ToClinic(clinic, Today);
    }

    public async Task<ClinicProfile> SaveAsync(
        Guid userId,
        Guid clinicId,
        string? name,
        ClinicType? type,
        string? legalName,
        string? description,
        string? phoneNumber,
        string? email,
        string? website,
        int? establishmentYear,
        string? registrationNumber,
        string? registrationAuthority,
        string? registrationState,
        DateOnly? registrationExpiresOn,
        CancellationToken cancellationToken)
    {
        var clinic = await OwnedClinicAsync(userId, clinicId, cancellationToken);
        clinic.UpdateDetails(
            name,
            type,
            legalName,
            description,
            phoneNumber,
            email,
            website,
            establishmentYear,
            registrationNumber,
            registrationAuthority,
            registrationState,
            registrationExpiresOn,
            clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return ProviderMapper.ToClinic(clinic, Today);
    }

    public async Task<ClinicProfile> AddBranchAsync(
        Guid userId,
        Guid clinicId,
        string name,
        string addressLine,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        decimal? latitude,
        decimal? longitude,
        string? phoneNumber,
        bool isPrimary,
        CancellationToken cancellationToken)
    {
        var clinic = await OwnedClinicAsync(userId, clinicId, cancellationToken);
        clinic.AddBranch(name, addressLine, addressLine2, city, state, postalCode, latitude, longitude, phoneNumber, isPrimary, clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return ProviderMapper.ToClinic(clinic, Today);
    }

    public async Task<ClinicProfile> UpdateBranchAsync(
        Guid userId,
        Guid clinicId,
        Guid branchId,
        string name,
        string addressLine,
        string? addressLine2,
        string city,
        string state,
        string postalCode,
        decimal? latitude,
        decimal? longitude,
        string? phoneNumber,
        bool isPrimary,
        CancellationToken cancellationToken)
    {
        var clinic = await OwnedClinicAsync(userId, clinicId, cancellationToken);
        clinic.UpdateBranch(
            branchId,
            name,
            addressLine,
            addressLine2,
            city,
            state,
            postalCode,
            latitude,
            longitude,
            phoneNumber,
            isPrimary,
            clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        return ProviderMapper.ToClinic(clinic, Today);
    }

    public async Task<ClinicProfile> AttachDocumentAsync(
        Guid userId,
        Guid clinicId,
        DocumentCategory category,
        StoredUpload upload,
        CancellationToken cancellationToken)
    {
        var clinic = await OwnedClinicAsync(userId, clinicId, cancellationToken);
        clinic.AttachDocument(
            category,
            upload.FileName,
            upload.ContentType,
            upload.SizeBytes,
            upload.StorageKey,
            upload.ScanEngine,
            upload.ScanDetail,
            clock.UtcNow);
        await store.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Stored clinic document {DocumentCategory} for clinic {ClinicId}", category, clinic.Id);
        return ProviderMapper.ToClinic(clinic, Today);
    }

    public async Task<ClinicProfile> SubmitAsync(
        Guid userId,
        Guid clinicId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var clinic = await OwnedClinicAsync(userId, clinicId, cancellationToken);
        var now = clock.UtcNow;
        clinic.Submit(now);
        await store.AddAuditAsync(
            AuditLog.Record(userId, AuditActions.ClinicSubmitted, now, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        notifier.ApplicationSubmitted("clinic", clinic.Reference);
        logger.LogInformation("Clinic application {Reference} submitted", clinic.Reference);
        return ProviderMapper.ToClinic(clinic, Today);
    }

    private async Task<Clinic> OwnedClinicAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(clinicId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (clinic.OwnerUserId != userId)
        {
            throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        }

        return clinic;
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
}
