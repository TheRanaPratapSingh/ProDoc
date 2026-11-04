using System.Text;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Providers;
using ProDoc.Application.Support;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Admin;

public sealed class AdminPlatformService(IAdminStore store, SupportService support, IClock clock)
{
    public Task<AdminDashboardMetrics> DashboardAsync(
        DateOnly? from,
        DateOnly? to,
        string? state,
        string? city,
        Guid? clinicId,
        string? appointmentStatus,
        CancellationToken cancellationToken) =>
        store.GetDashboardAsync(from, to, state, city, clinicId, appointmentStatus, cancellationToken);

    public Task<IReadOnlyList<AdminUserSummary>> SearchUsersAsync(string? query, string? role, bool? suspended, CancellationToken cancellationToken) =>
        store.SearchUsersAsync(query, role, suspended, 100, cancellationToken);

    public async Task SuspendUserAsync(Guid actorUserId, Guid userId, string? reason, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        if (actorUserId == userId)
        {
            throw DomainException.Validation("cannot_suspend_self", "You cannot suspend your own account.");
        }

        await store.SuspendUserAsync(userId, reason, clock.UtcNow, cancellationToken);
        await store.RevokeRefreshTokensAsync(userId, clock.UtcNow, cancellationToken);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.AccountSuspended, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task ReactivateUserAsync(Guid actorUserId, Guid userId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        await store.ReactivateUserAsync(userId, cancellationToken);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.AccountReactivated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantRoleAsync(Guid actorUserId, Guid userId, string roleName, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        EnsureAssignableRole(roleName);
        await store.GrantRoleAsync(userId, roleName, cancellationToken);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.RoleGranted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeRoleAsync(Guid actorUserId, Guid userId, string roleName, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        EnsureAssignableRole(roleName);
        if (roleName == RoleNames.PlatformAdmin && actorUserId == userId)
        {
            throw DomainException.Validation("cannot_revoke_own_admin", "You cannot revoke your own platform admin role.");
        }

        await store.RevokeRoleAsync(userId, roleName, cancellationToken);
        await store.AddAuditAsync(Audit(actorUserId, AuditActions.RoleRevoked, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AuditEntryDetails>> ListAuditAsync(string? action, Guid? actorUserId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        store.ListAuditAsync(action, actorUserId, from, to, 200, cancellationToken);

    public Task<IReadOnlyList<ProviderDirectoryItem>> ListProvidersAsync(string kind, string? status, string? city, string? state, CancellationToken cancellationToken) =>
        store.ListProvidersAsync(kind, status, city, state, 200, cancellationToken);

    public Task<SupportTicketDetails> CreateTicketAsync(
        Guid userId,
        string category,
        string priority,
        string subject,
        string description,
        Guid? appointmentId,
        Guid? paymentId,
        Guid? clinicId,
        RequestMetadata metadata,
        CancellationToken cancellationToken) =>
        support.CreateTicketAsync(userId, category, priority, subject, description, appointmentId, paymentId, clinicId, metadata, cancellationToken);

    public Task<IReadOnlyList<SupportTicketSummary>> ListMyTicketsAsync(Guid userId, CancellationToken cancellationToken) =>
        support.ListMyTicketsAsync(userId, cancellationToken);

    public async Task<IReadOnlyList<SupportTicketSummary>> ListAllTicketsAsync(SupportTicketStatus? status, CancellationToken cancellationToken)
    {
        var workspace = await support.AgentWorkspaceAsync(Guid.Empty, status?.ToString(), null, "all", cancellationToken);
        return workspace.Tickets;
    }

    public Task<SupportTicketDetails> GetTicketAsync(Guid userId, Guid ticketId, bool isAdmin, CancellationToken cancellationToken) =>
        support.GetTicketAsync(userId, ticketId, isAdmin, cancellationToken);

    public Task<SupportTicketDetails> UpdateTicketAsync(
        Guid adminUserId,
        Guid ticketId,
        string? status,
        string? priority,
        Guid? assignedToUserId,
        string? note,
        bool internalNote,
        RequestMetadata metadata,
        CancellationToken cancellationToken) =>
        support.UpdateTicketAsync(
            adminUserId,
            ticketId,
            status,
            priority,
            assignedToUserId,
            note,
            internalNote,
            resolutionDetails: null,
            escalate: false,
            metadata,
            cancellationToken);

    public Task<SupportTicketDetails> AddUserNoteAsync(Guid userId, Guid ticketId, string note, RequestMetadata metadata, CancellationToken cancellationToken) =>
        support.AddUserNoteAsync(userId, ticketId, note, metadata, cancellationToken);

    public Task<IReadOnlyList<PlatformSetting>> RawSettingsAsync(CancellationToken cancellationToken) =>
        store.ListSettingsAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformSettingDetails>> ListSettingsAsync(CancellationToken cancellationToken)
    {
        var rows = await store.ListSettingsAsync(cancellationToken);
        return rows.Select(item => new PlatformSettingDetails(item.Id, item.Key, item.Value, item.Description, item.UpdatedAtUtc)).ToArray();
    }

    public async Task<PlatformSettingDetails> UpsertSettingAsync(Guid userId, string key, string value, string? description, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var existing = await store.FindSettingAsync(key, true, cancellationToken);
        if (existing is null)
        {
            var created = PlatformSetting.Create(key, value, description, userId, clock.UtcNow);
            store.AddSetting(created);
            existing = created;
        }
        else
        {
            existing.Update(value, userId, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.PlatformSettingChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new PlatformSettingDetails(existing.Id, existing.Key, existing.Value, existing.Description, existing.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<FeatureFlagDetails>> ListFlagsAsync(CancellationToken cancellationToken)
    {
        var rows = await store.ListFlagsAsync(cancellationToken);
        return rows.Select(item => new FeatureFlagDetails(item.Id, item.Key, item.Enabled, item.Description, item.UpdatedAtUtc)).ToArray();
    }

    public async Task<FeatureFlagDetails> UpsertFlagAsync(Guid userId, string key, bool enabled, string? description, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var existing = await store.FindFlagAsync(key, true, cancellationToken);
        if (existing is null)
        {
            var created = FeatureFlag.Create(key, enabled, description, userId, clock.UtcNow);
            store.AddFlag(created);
            existing = created;
        }
        else
        {
            existing.SetEnabled(enabled, userId, clock.UtcNow);
        }

        await store.AddAuditAsync(Audit(userId, AuditActions.FeatureFlagChanged, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new FeatureFlagDetails(existing.Id, existing.Key, existing.Enabled, existing.Description, existing.UpdatedAtUtc);
    }

    public Task<IReadOnlyList<FinanceReportRow>> FinanceReportAsync(DateOnly? from, DateOnly? to, string? state, string? city, Guid? clinicId, CancellationToken cancellationToken) =>
        store.FinanceReportAsync(from, to, state, city, clinicId, cancellationToken);

    public async Task<byte[]> ExportFinanceCsvAsync(DateOnly? from, DateOnly? to, string? state, string? city, Guid? clinicId, CancellationToken cancellationToken)
    {
        var rows = await store.FinanceReportAsync(from, to, state, city, clinicId, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("appointmentId,clinicId,clinicName,city,state,consultationFee,commissionAmount,clinicPayable,capturedAtUtc,walkInExcluded");
        foreach (var row in rows)
        {
            builder.Append(row.AppointmentId).Append(',')
                .Append(row.ClinicId).Append(',')
                .Append(Csv(row.ClinicName)).Append(',')
                .Append(Csv(row.City)).Append(',')
                .Append(Csv(row.State)).Append(',')
                .Append(row.ConsultationFee).Append(',')
                .Append(row.CommissionAmount).Append(',')
                .Append(row.ClinicPayable).Append(',')
                .Append(row.CapturedAtUtc.ToString("O")).Append(',')
                .Append(row.IsWalkInExcluded).AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        var settings = await store.ListSettingsAsync(cancellationToken);
        if (settings.Count == 0)
        {
            store.AddSetting(PlatformSetting.Create("appointments.patient_change_hours", "2", "Hours before start when patients may cancel/reschedule", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("platform.maintenance", "false", "When true, clients should show maintenance messaging", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("platform.supported_languages", "en,hi", "Supported UI languages", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("reviews.edit_hours", "48", "Hours after submission when patients may edit a review", null, clock.UtcNow));
        }
        else if (settings.All(item => item.Key != "reviews.edit_hours"))
        {
            store.AddSetting(PlatformSetting.Create("reviews.edit_hours", "48", "Hours after submission when patients may edit a review", null, clock.UtcNow));
        }

        if (settings.All(item => item.Key != "growth.referral_reward_points"))
        {
            store.AddSetting(PlatformSetting.Create("growth.referral_reward_points", "100", "Points awarded to referrer after conversion", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("growth.referral_reward_credit", "0", "INR credit awarded to referrer after conversion", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("growth.loyalty_points_per_visit", "20", "Loyalty points for a paid completed visit", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("growth.max_referral_rewards_per_day", "20", "Max referral rewards a user can earn per day", null, clock.UtcNow));
            store.AddSetting(PlatformSetting.Create("growth.campaign_cooldown_days", "7", "Days before the same campaign may message a user again", null, clock.UtcNow));
        }

        var flags = await store.ListFlagsAsync(cancellationToken);
        if (flags.Count == 0)
        {
            store.AddFlag(FeatureFlag.Create("payments.online", true, "Allow online payment checkout", null, clock.UtcNow));
            store.AddFlag(FeatureFlag.Create("support.tickets", true, "Allow users to open support tickets", null, clock.UtcNow));
            store.AddFlag(FeatureFlag.Create("assistant.enabled", true, "Allow patients to use ProDoc Assistant", null, clock.UtcNow));
            store.AddFlag(FeatureFlag.Create("growth.referrals", true, "Enable patient referral programme", null, clock.UtcNow));
            store.AddFlag(FeatureFlag.Create("growth.coupons", true, "Enable coupon engine at checkout", null, clock.UtcNow));
        }
        else
        {
            if (flags.All(item => item.Key != "assistant.enabled"))
            {
                store.AddFlag(FeatureFlag.Create("assistant.enabled", true, "Allow patients to use ProDoc Assistant", null, clock.UtcNow));
            }

            if (flags.All(item => item.Key != "growth.referrals"))
            {
                store.AddFlag(FeatureFlag.Create("growth.referrals", true, "Enable patient referral programme", null, clock.UtcNow));
            }

            if (flags.All(item => item.Key != "growth.coupons"))
            {
                store.AddFlag(FeatureFlag.Create("growth.coupons", true, "Enable coupon engine at checkout", null, clock.UtcNow));
            }
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureAssignableRole(string roleName)
    {
        if (roleName is not (RoleNames.PlatformAdmin or RoleNames.ClinicAdmin or RoleNames.OrganizationAdmin or RoleNames.Receptionist or RoleNames.ClinicStaff or RoleNames.Doctor or RoleNames.Patient))
        {
            throw DomainException.Validation("role_invalid", "Choose a known platform role.");
        }
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
