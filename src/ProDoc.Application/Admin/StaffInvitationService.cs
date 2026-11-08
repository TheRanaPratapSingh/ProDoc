using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Auth;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;

namespace ProDoc.Application.Admin;

public sealed class StaffInvitationService(
    IStaffInvitationStore invitations,
    IAuthStore auth,
    IOtpCodeProtector otpProtector,
    IOtpSender otpSender,
    IClock clock,
    IOptions<SessionOptions> options,
    ILogger<StaffInvitationService> logger)
{
    private readonly SessionOptions _options = options.Value;
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromHours(48);

    public async Task<StaffInvitationDetails> InviteAsync(InviteStaffCommand command, CancellationToken cancellationToken)
    {
        var role = StaffInviteRoles.Normalize(command.RoleName);
        var phone = PhoneNumber.ParseIndianMobile(command.PhoneNumber);
        var now = clock.UtcNow;
        var clinicIds = NormalizeIds(command.ClinicIds, command.ClinicId);
        var doctorIds = NormalizeIds(command.DoctorIds, null);

        if (role is RoleNames.Receptionist or RoleNames.ClinicStaff or RoleNames.ClinicAdmin)
        {
            if (clinicIds.Count == 0)
            {
                throw DomainException.Validation("invite_clinic_required", "Assign a clinic for clinic staff roles.");
            }
        }

        if (role == RoleNames.Doctor && clinicIds.Count == 0)
        {
            throw DomainException.Validation("invite_clinic_required", "Assign at least one clinic for the doctor.");
        }

        if (role == RoleNames.Doctor && string.IsNullOrWhiteSpace(command.RegistrationNumber))
        {
            throw DomainException.Validation(
                "invite_registration_required",
                "Enter the doctor's medical registration number so the profile can be activated for patient search.");
        }

        if (role == RoleNames.Doctor && command.SpecializationId is null)
        {
            throw DomainException.Validation("invite_specialization_required", "Choose a specialization for the doctor.");
        }

        if (role == RoleNames.Receptionist && doctorIds.Count == 0)
        {
            throw DomainException.Validation("invite_doctors_required", "Assign at least one doctor for the receptionist.");
        }

        if (role == RoleNames.ClinicStaff && string.IsNullOrWhiteSpace(command.Designation))
        {
            throw DomainException.Validation("invite_designation_required", "Enter a staff designation.");
        }

        foreach (var clinicId in clinicIds)
        {
            if (!await invitations.ClinicExistsAsync(clinicId, cancellationToken))
            {
                throw DomainException.Validation(
                    "clinic_not_eligible",
                    "Choose an approved clinic with an active branch.");
            }
        }

        foreach (var doctorId in doctorIds)
        {
            if (!await invitations.DoctorExistsAsync(doctorId, cancellationToken))
            {
                throw DomainException.NotFound("doctor_not_found", "That doctor was not found.");
            }
        }

        var existingAccount = await auth.FindAccountByPhoneAsync(phone.E164, cancellationToken);
        if (existingAccount?.Roles.Contains(RoleNames.PlatformAdmin, StringComparer.Ordinal) == true)
        {
            throw DomainException.Conflict("invite_admin_phone", "That mobile number belongs to a platform administrator.");
        }

        if (existingAccount?.Roles.Contains(role, StringComparer.Ordinal) == true
            && role is RoleNames.Doctor or RoleNames.ClinicAdmin)
        {
            throw DomainException.Conflict("invite_role_exists", "That account already has this role.");
        }

        var pending = await invitations.FindPendingByPhoneAsync(phone.E164, now, cancellationToken);
        if (pending is not null)
        {
            throw DomainException.Conflict("invite_pending", "A pending invitation already exists for this mobile number.");
        }

        if (role == RoleNames.Doctor)
        {
            if (command.YearsOfExperience is null or < 0 or > 70)
            {
                throw DomainException.Validation(
                    "invite_experience_required",
                    "Enter years of experience as a whole number between 0 and 70.");
            }
        }

        var payload = new StaffInvitePayload
        {
            SpecializationId = command.SpecializationId,
            YearsOfExperience = role == RoleNames.Doctor ? command.YearsOfExperience : null,
            Qualifications = string.IsNullOrWhiteSpace(command.Qualifications) ? null : command.Qualifications.Trim(),
            RegistrationNumber = string.IsNullOrWhiteSpace(command.RegistrationNumber) ? null : command.RegistrationNumber.Trim(),
            Designation = string.IsNullOrWhiteSpace(command.Designation) ? null : command.Designation.Trim(),
            ClinicIds = clinicIds,
            DoctorIds = doctorIds
        };

        var invitation = StaffInvitation.Create(
            phone.E164,
            command.FullName,
            command.Email,
            role,
            clinicIds.Count > 0 ? clinicIds[0] : null,
            command.BranchId,
            command.ActorUserId,
            now,
            InviteLifetime,
            payload.ToJson());
        await invitations.AddAsync(invitation, cancellationToken);
        await invitations.AddAuditAsync(
            AuditLog.Record(command.ActorUserId, AuditActions.StaffInvited, now, command.IpAddress, command.UserAgent),
            cancellationToken);
        var otp = await DispatchInviteOtpAsync(phone.E164, now, cancellationToken);
        await invitations.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Staff invitation {InvitationId} created for role {Role}", invitation.Id, role);
        return Map(invitation, otp);
    }

    public async Task<StaffInvitationDetails> ResendAsync(
        Guid actorUserId,
        Guid invitationId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindByIdAsync(invitationId, cancellationToken)
            ?? throw DomainException.NotFound("invite_not_found", "That invitation was not found.");
        if (!invitation.IsPending(clock.UtcNow))
        {
            throw DomainException.Validation("invite_not_pending", "Only pending invitations can receive a new OTP.");
        }

        var now = clock.UtcNow;
        invitation.Extend(now, InviteLifetime);
        await invitations.AddAuditAsync(
            AuditLog.Record(actorUserId, AuditActions.StaffInviteResent, now, ipAddress, userAgent),
            cancellationToken);
        var otp = await DispatchInviteOtpAsync(invitation.PhoneE164, now, cancellationToken);
        await invitations.SaveChangesAsync(cancellationToken);
        return Map(invitation, otp);
    }

    public async Task<StaffInviteActivationResult> VerifyInviteAsync(
        Guid actorUserId,
        Guid invitationId,
        string code,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindByIdAsync(invitationId, cancellationToken)
            ?? throw DomainException.NotFound("invite_not_found", "That invitation was not found.");
        var now = clock.UtcNow;

        // Idempotent: a second verify after success returns the already-activated account.
        // If a prior accept left a doctor non-discoverable, repair activation from invite payload.
        if (invitation.Status == StaffInvitationStatus.Accepted && invitation.AcceptedUserId is Guid acceptedUserId)
        {
            if (invitation.RoleName == RoleNames.Doctor)
            {
                await RepairDoctorActivationAsync(invitation, acceptedUserId, now, cancellationToken);
            }

            return await BuildActivationResultAsync(invitation, acceptedUserId, cancellationToken);
        }

        invitation.EnsureAcceptable(now);

        var submittedCode = code?.Trim() ?? string.Empty;
        var challenge = await auth.FindLatestOtpChallengeAsync(invitation.PhoneE164, OtpPurpose.StaffInvite, cancellationToken);
        if (challenge is null || !challenge.IsActive(now) || !otpProtector.Verify(submittedCode, challenge.CodeHash))
        {
            if (challenge is not null && challenge.IsActive(now))
            {
                challenge.RegisterFailedAttempt();
                await invitations.SaveChangesAsync(cancellationToken);
            }

            throw DomainException.Validation("otp_invalid", "Incorrect OTP. Please try again.");
        }

        challenge.Consume(now);
        var account = await AcceptPendingInviteAsync(invitation.PhoneE164, now, cancellationToken);

        logger.LogInformation(
            "Staff invitation {InvitationId} verified by admin {ActorUserId} for user {UserId}",
            invitation.Id,
            actorUserId,
            account.UserId);
        return await BuildActivationResultAsync(invitation, account.UserId, cancellationToken);
    }

    public async Task CancelAsync(
        Guid actorUserId,
        Guid invitationId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindByIdAsync(invitationId, cancellationToken)
            ?? throw DomainException.NotFound("invite_not_found", "That invitation was not found.");
        var now = clock.UtcNow;
        invitation.Cancel(now);
        await invitations.AddAuditAsync(
            AuditLog.Record(actorUserId, AuditActions.StaffInviteCancelled, now, ipAddress, userAgent),
            cancellationToken);
        await invitations.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<StaffInvitationDetails>> ListAsync(string? status, CancellationToken cancellationToken) =>
        invitations.ListAsync(status, 100, cancellationToken);

    public async Task<AccountSnapshot> AcceptPendingInviteAsync(
        string phoneE164,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindPendingByPhoneAsync(phoneE164, now, cancellationToken)
            ?? throw DomainException.Validation("invite_not_found", "No pending staff invitation was found for this number.");
        invitation.EnsureAcceptable(now);
        var payload = StaffInvitePayload.Parse(invitation.PayloadJson);
        var clinicIds = NormalizeIds(payload?.ClinicIds, invitation.ClinicId);
        var doctorIds = NormalizeIds(payload?.DoctorIds, null);

        var account = await auth.FindAccountByPhoneAsync(phoneE164, cancellationToken);
        if (account is null)
        {
            account = await auth.CreatePatientAccountAsync(
                phoneE164,
                [RoleNames.Patient, invitation.RoleName],
                now,
                cancellationToken);
        }
        else
        {
            if (account.SuspendedAtUtc is not null)
            {
                throw DomainException.Validation("account_suspended", "This account is suspended. Contact ProDoc support.");
            }

            await invitations.GrantRoleAsync(account.UserId, invitation.RoleName, cancellationToken);
        }

        // Always refresh profile + clear deletion-pending so discovery and login stay consistent.
        await invitations.EnsurePatientProfileAsync(account.UserId, invitation.FullName, now, cancellationToken);

        if (invitation.RoleName == RoleNames.Doctor)
        {
            await invitations.EnsureDoctorDraftAsync(account.UserId, invitation.FullName, now, cancellationToken);
            await invitations.ApplyDoctorInviteProfileAsync(
                account.UserId,
                invitation.FullName,
                payload?.SpecializationId,
                payload?.YearsOfExperience,
                payload?.Qualifications,
                payload?.RegistrationNumber,
                now,
                cancellationToken);
            // ActivateDoctorForPublicDiscoveryAsync affiliates clinics idempotently before publishing.
            await invitations.ActivateDoctorForPublicDiscoveryAsync(
                account.UserId,
                clinicIds,
                invitation.BranchId,
                now,
                cancellationToken);
        }

        if (clinicIds.Count > 0)
        {
            if (invitation.RoleName is RoleNames.Receptionist or RoleNames.ClinicStaff)
            {
                foreach (var clinicId in clinicIds)
                {
                    await invitations.AssignClinicStaffAsync(
                        clinicId,
                        account.UserId,
                        invitation.BranchId,
                        invitation.RoleName,
                        now,
                        cancellationToken);
                }
            }
            else if (invitation.RoleName == RoleNames.ClinicAdmin)
            {
                await invitations.TransferClinicOwnershipAsync(clinicIds[0], account.UserId, now, cancellationToken);
            }
        }

        if (invitation.RoleName == RoleNames.Receptionist && doctorIds.Count > 0 && clinicIds.Count > 0)
        {
            foreach (var clinicId in clinicIds)
            {
                await invitations.AssignReceptionistDoctorsAsync(clinicId, account.UserId, doctorIds, now, cancellationToken);
            }
        }

        invitation.Accept(account.UserId, now);
        await invitations.AddAuditAsync(
            AuditLog.Record(account.UserId, AuditActions.StaffInviteAccepted, now, null, null),
            cancellationToken);
        await invitations.SaveChangesAsync(cancellationToken);

        return await auth.FindAccountByUserIdAsync(account.UserId, cancellationToken)
               ?? throw DomainException.NotFound("account_not_found", "Account was not found.");
    }

    private async Task<InviteOtpDispatch> DispatchInviteOtpAsync(
        string phoneE164,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var since = now.AddMinutes(-_options.OtpRequestWindowMinutes);
        var recent = await auth.CountRecentOtpChallengesAsync(phoneE164, since, cancellationToken);
        if (recent >= _options.OtpMaxRequestsPerWindow)
        {
            throw DomainException.TooManyRequests("otp_rate_limited", "Too many codes were requested. Wait a few minutes and try again.");
        }

        var latest = await auth.FindLatestOtpChallengeAsync(phoneE164, OtpPurpose.StaffInvite, cancellationToken);
        if (latest is not null && latest.CreatedAtUtc.AddSeconds(_options.OtpResendCooldownSeconds) > now)
        {
            throw DomainException.TooManyRequests(
                "otp_cooldown",
                "A code was just sent. Wait a moment before requesting another.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = OtpChallenge.Issue(
            phoneE164,
            otpProtector.Hash(code),
            OtpPurpose.StaffInvite,
            now,
            TimeSpan.FromMinutes(_options.OtpLifetimeMinutes),
            _options.OtpMaxAttempts);
        await auth.AddOtpChallengeAsync(challenge, cancellationToken);
        await otpSender.SendSignInCodeAsync(phoneE164, code, cancellationToken);
        return new InviteOtpDispatch(
            challenge.Id,
            challenge.ExpiresAtUtc,
            _options.ExposeOtpInResponse ? code : null);
    }

    private async Task<StaffInviteActivationResult> BuildActivationResultAsync(
        StaffInvitation invitation,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var payload = StaffInvitePayload.Parse(invitation.PayloadJson);
        var clinicIds = NormalizeIds(payload?.ClinicIds, invitation.ClinicId);
        Guid? doctorId = null;
        string? doctorStatus = null;
        var discoverable = false;

        if (invitation.RoleName == RoleNames.Doctor)
        {
            var state = await invitations.GetDoctorDiscoveryStateAsync(userId, cancellationToken);
            if (state is not null)
            {
                doctorId = state.Value.DoctorId;
                doctorStatus = state.Value.Status;
                discoverable = state.Value.IsPubliclyDiscoverable;
            }
        }

        return new StaffInviteActivationResult(
            invitation.Id,
            userId,
            invitation.FullName,
            invitation.PhoneE164,
            invitation.RoleName,
            clinicIds,
            doctorId,
            doctorStatus,
            discoverable);
    }

    private async Task RepairDoctorActivationAsync(
        StaffInvitation invitation,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await invitations.GetDoctorDiscoveryStateAsync(userId, cancellationToken);
        if (state?.IsPubliclyDiscoverable == true)
        {
            return;
        }

        var payload = StaffInvitePayload.Parse(invitation.PayloadJson);
        var clinicIds = NormalizeIds(payload?.ClinicIds, invitation.ClinicId);
        await invitations.EnsureDoctorDraftAsync(userId, invitation.FullName, now, cancellationToken);
        // Re-apply invite essentials only while the doctor is not yet Approved; changing
        // registration on an Approved profile would bounce it back to Submitted.
        if (state is null || !string.Equals(state.Value.Status, nameof(VerificationStatus.Approved), StringComparison.Ordinal))
        {
            await invitations.ApplyDoctorInviteProfileAsync(
                userId,
                invitation.FullName,
                payload?.SpecializationId,
                payload?.YearsOfExperience,
                payload?.Qualifications,
                payload?.RegistrationNumber,
                now,
                cancellationToken);
        }

        if (clinicIds.Count > 0)
        {
            await invitations.AffiliateDoctorToClinicsAsync(userId, clinicIds, invitation.BranchId, now, cancellationToken);
        }

        await invitations.ActivateDoctorForPublicDiscoveryAsync(
            userId,
            clinicIds,
            invitation.BranchId,
            now,
            cancellationToken);
        await invitations.SaveChangesAsync(cancellationToken);
    }

    private static List<Guid> NormalizeIds(IReadOnlyList<Guid>? ids, Guid? fallback)
    {
        var set = new HashSet<Guid>();
        if (ids is not null)
        {
            foreach (var id in ids.Where(item => item != Guid.Empty))
            {
                set.Add(id);
            }
        }

        if (fallback is Guid value && value != Guid.Empty)
        {
            set.Add(value);
        }

        return set.ToList();
    }

    private static StaffInvitationDetails Map(StaffInvitation invitation, InviteOtpDispatch? otp = null)
    {
        var payload = StaffInvitePayload.Parse(invitation.PayloadJson);
        return new(
            invitation.Id,
            invitation.PhoneE164,
            invitation.FullName,
            invitation.Email,
            invitation.RoleName,
            invitation.ClinicId,
            invitation.BranchId,
            invitation.Status.ToString(),
            invitation.CreatedAtUtc,
            invitation.ExpiresAtUtc,
            invitation.AcceptedAtUtc,
            payload?.SpecializationId,
            payload?.YearsOfExperience,
            payload?.Qualifications,
            payload?.RegistrationNumber,
            payload?.Designation,
            payload?.ClinicIds,
            payload?.DoctorIds,
            otp?.ExpiresAtUtc,
            otp?.DevelopmentCode,
            otp?.ChallengeId);
    }

    private sealed record InviteOtpDispatch(Guid ChallengeId, DateTimeOffset ExpiresAtUtc, string? DevelopmentCode);
}
