using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Admin;

public sealed class AdminClinicService(IAdminClinicStore store, IClock clock)
{
    public Task<IReadOnlyList<AdminClinicSummary>> ListAsync(string? query, CancellationToken cancellationToken) =>
        store.ListClinicsAsync(query, 200, cancellationToken);

    public async Task<AdminClinicDetails> GetAsync(Guid clinicId, CancellationToken cancellationToken) =>
        await store.GetClinicDetailsAsync(clinicId, cancellationToken)
        ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");

    public async Task<AdminClinicDetails> CreateAsync(CreateAdminClinicCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var type = ParseClinicType(command.Type);
        var clinic = Clinic.Begin(command.ActorUserId, now);
        var clinicName = command.Name.Trim();
        var email = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        var website = string.IsNullOrWhiteSpace(command.Website) ? null : command.Website.Trim();
        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        var registrationNumber = string.IsNullOrWhiteSpace(command.RegistrationNumber) ? null : command.RegistrationNumber.Trim();
        // Branch name is optional in the admin form; when omitted, reuse the clinic name (not a placeholder).
        var branchName = string.IsNullOrWhiteSpace(command.BranchName) ? clinicName : command.BranchName.Trim();
        var addressLine2 = string.IsNullOrWhiteSpace(command.AddressLine2) ? null : command.AddressLine2.Trim();
        var landmark = string.IsNullOrWhiteSpace(command.Landmark) ? null : command.Landmark.Trim();
        var locality = string.IsNullOrWhiteSpace(command.Locality) ? null : command.Locality.Trim();
        var district = string.IsNullOrWhiteSpace(command.District) ? null : command.District.Trim();
        var country = string.IsNullOrWhiteSpace(command.Country) ? null : command.Country.Trim();
        var googlePlaceId = string.IsNullOrWhiteSpace(command.GooglePlaceId) ? null : command.GooglePlaceId.Trim();

        clinic.UpdateDetails(
            clinicName,
            type,
            null,
            description,
            command.PhoneNumber,
            email,
            website,
            null,
            registrationNumber,
            null,
            null,
            null,
            now);
        clinic.AddBranch(
            branchName,
            command.AddressLine,
            addressLine2,
            command.City,
            command.State,
            command.PostalCode,
            command.Latitude,
            command.Longitude,
            command.BranchPhoneNumber ?? command.PhoneNumber,
            isPrimary: true,
            now,
            landmark,
            locality,
            district,
            country,
            googlePlaceId);
        clinic.ProvisionApproved(now);

        await store.AddClinicAsync(clinic, cancellationToken);
        await store.AddAuditAsync(
            AuditLog.Record(command.ActorUserId, AuditActions.AdminClinicCreated, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetAsync(clinic.Id, cancellationToken);
    }

    public async Task<AdminClinicDetails> UpdateAsync(UpdateAdminClinicCommand command, CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(command.ClinicId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        var now = clock.UtcNow;
        var type = ParseClinicType(command.Type);

        var email = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        var website = string.IsNullOrWhiteSpace(command.Website) ? null : command.Website.Trim();
        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        var registrationNumber = string.IsNullOrWhiteSpace(command.RegistrationNumber) ? null : command.RegistrationNumber.Trim();

        // Avoid kicking approved clinics back into verification for admin name/contact edits.
        if (clinic.Status == VerificationStatus.Approved)
        {
            clinic.UpdateDetails(
                command.Name,
                type,
                clinic.LegalName,
                description,
                command.PhoneNumber,
                email,
                website,
                clinic.EstablishmentYear,
                registrationNumber,
                clinic.RegistrationAuthority,
                clinic.RegistrationState,
                clinic.RegistrationExpiresOn,
                now);
            if (clinic.Status != VerificationStatus.Approved)
            {
                clinic.ProvisionApproved(now);
            }
        }
        else
        {
            clinic.UpdateDetails(
                command.Name,
                type,
                clinic.LegalName,
                description,
                command.PhoneNumber,
                email,
                website,
                clinic.EstablishmentYear,
                registrationNumber,
                clinic.RegistrationAuthority,
                clinic.RegistrationState,
                clinic.RegistrationExpiresOn,
                now);
            if (clinic.Status is VerificationStatus.Draft or VerificationStatus.AdditionalInformationRequired)
            {
                clinic.ProvisionApproved(now);
            }
        }

        await store.AddAuditAsync(
            AuditLog.Record(command.ActorUserId, AuditActions.AdminClinicUpdated, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetAsync(clinic.Id, cancellationToken);
    }

    public async Task<AdminClinicDetails> UpdateBranchAsync(UpdateAdminClinicBranchCommand command, CancellationToken cancellationToken)
    {
        var clinic = await store.FindClinicAsync(command.ClinicId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        var now = clock.UtcNow;
        var wasApproved = clinic.Status == VerificationStatus.Approved;
        clinic.UpdateBranch(
            command.BranchId,
            command.Name,
            command.AddressLine,
            command.AddressLine2,
            command.City,
            command.State,
            command.PostalCode,
            command.Latitude,
            command.Longitude,
            command.PhoneNumber,
            command.IsPrimary,
            now,
            command.Landmark,
            command.Locality,
            command.District,
            command.Country,
            command.GooglePlaceId);
        if (wasApproved && clinic.Status != VerificationStatus.Approved)
        {
            clinic.ProvisionApproved(now);
        }

        await store.AddAuditAsync(
            AuditLog.Record(command.ActorUserId, AuditActions.AdminClinicUpdated, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetAsync(clinic.Id, cancellationToken);
    }

    public async Task UpdatePracticeLocationAsync(UpdateDoctorPracticeLocationCommand command, CancellationToken cancellationToken)
    {
        var affiliation = await store.FindAffiliationAsync(command.AffiliationId, tracking: true, cancellationToken)
            ?? throw DomainException.NotFound("affiliation_not_found", "That clinic association was not found.");
        if (affiliation.DoctorId != command.DoctorId)
        {
            throw DomainException.NotFound("affiliation_not_found", "That clinic association was not found.");
        }

        var now = clock.UtcNow;
        if (command.ClearOverride)
        {
            affiliation.ClearPracticeLocation();
        }
        else
        {
            affiliation.SetPracticeLocation(
                command.AddressLine ?? string.Empty,
                command.AddressLine2,
                command.Landmark,
                command.Locality,
                command.City ?? string.Empty,
                command.District,
                command.State ?? string.Empty,
                command.PostalCode ?? string.Empty,
                command.Country,
                command.Latitude,
                command.Longitude,
                command.GooglePlaceId,
                now);
        }

        await store.AddAuditAsync(
            AuditLog.Record(command.ActorUserId, AuditActions.AdminClinicUpdated, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignDoctorAsync(
        Guid actorUserId,
        Guid clinicId,
        Guid doctorId,
        Guid? branchId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        _ = await store.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (!await store.DoctorExistsAsync(doctorId, cancellationToken))
        {
            throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");
        }

        var now = clock.UtcNow;
        await store.AffiliateDoctorAsync(clinicId, doctorId, branchId, now, cancellationToken);
        await store.ActivateDoctorDiscoveryAsync(doctorId, now, cancellationToken);
        await store.AddAuditAsync(
            AuditLog.Record(actorUserId, AuditActions.AdminClinicDoctorAssigned, now, ipAddress, userAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateDoctorDiscoveryAsync(
        Guid actorUserId,
        Guid doctorId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (!await store.DoctorExistsAsync(doctorId, cancellationToken))
        {
            throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");
        }

        var now = clock.UtcNow;
        await store.ActivateDoctorDiscoveryAsync(doctorId, now, cancellationToken);
        await store.AddAuditAsync(
            AuditLog.Record(actorUserId, AuditActions.AdminClinicDoctorAssigned, now, ipAddress, userAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveDoctorAsync(
        Guid actorUserId,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        await store.RemoveDoctorAffiliationAsync(clinicId, doctorId, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignStaffAsync(
        Guid actorUserId,
        Guid clinicId,
        Guid userId,
        string roleName,
        Guid? branchId,
        IReadOnlyList<Guid>? doctorIds,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        _ = await store.FindClinicAsync(clinicId, tracking: false, cancellationToken)
            ?? throw DomainException.NotFound("clinic_not_found", "That clinic was not found.");
        if (!await store.UserExistsAsync(userId, cancellationToken))
        {
            throw DomainException.NotFound("user_not_found", "That user was not found.");
        }

        var role = StaffInviteRoles.Normalize(roleName);
        if (role is not (RoleNames.Receptionist or RoleNames.ClinicStaff))
        {
            throw DomainException.Validation("staff_role_invalid", "Choose Receptionist or ClinicStaff.");
        }

        var now = clock.UtcNow;
        await store.AssignStaffAsync(clinicId, userId, branchId, role, now, cancellationToken);
        if (role == RoleNames.Receptionist && doctorIds is { Count: > 0 })
        {
            await store.AssignReceptionistDoctorsAsync(clinicId, userId, doctorIds, now, cancellationToken);
        }

        await store.AddAuditAsync(
            AuditLog.Record(actorUserId, AuditActions.AdminClinicStaffAssigned, now, ipAddress, userAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveStaffAsync(Guid clinicId, Guid userId, CancellationToken cancellationToken)
    {
        await store.DeactivateStaffAsync(clinicId, userId, clock.UtcNow, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static ClinicType ParseClinicType(string type)
    {
        if (Enum.TryParse<ClinicType>(type, true, out var parsed))
        {
            return parsed;
        }

        throw DomainException.Validation("clinic_type_invalid", "Choose Clinic, Hospital, or IndividualPractice.");
    }
}
