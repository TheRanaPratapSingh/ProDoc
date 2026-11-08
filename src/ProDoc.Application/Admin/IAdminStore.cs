using ProDoc.Domain.Admin;
using ProDoc.Domain.Governance;

namespace ProDoc.Application.Admin;

public interface IAdminStore
{
    Task<AdminDashboardMetrics> GetDashboardAsync(
        DateOnly? from,
        DateOnly? to,
        string? state,
        string? city,
        Guid? clinicId,
        string? appointmentStatus,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminUserSummary>> SearchUsersAsync(string? query, string? role, bool? suspended, Guid? clinicId, int take, CancellationToken cancellationToken);

    Task<AdminUserDetails?> GetUserDetailsAsync(Guid userId, CancellationToken cancellationToken);

    Task PermanentDeleteUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task SuspendUserAsync(Guid userId, string? reason, DateTimeOffset now, CancellationToken cancellationToken);

    Task ReactivateUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RevokeRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    Task RevokeRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntryDetails>> ListAuditAsync(string? action, Guid? actorUserId, DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProviderDirectoryItem>> ListProvidersAsync(string kind, string? status, string? city, string? state, int take, CancellationToken cancellationToken);

    Task<SupportTicket?> FindTicketAsync(Guid ticketId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupportTicket>> ListTicketsAsync(Guid? createdByUserId, SupportTicketStatus? status, int take, CancellationToken cancellationToken);

    void AddTicket(SupportTicket ticket);

    void AddTicketNote(SupportTicketNote note);

    Task<string> NextTicketNumberAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformSetting>> ListSettingsAsync(CancellationToken cancellationToken);

    Task<PlatformSetting?> FindSettingAsync(string key, bool tracking, CancellationToken cancellationToken);

    void AddSetting(PlatformSetting setting);

    Task<IReadOnlyList<FeatureFlag>> ListFlagsAsync(CancellationToken cancellationToken);

    Task<FeatureFlag?> FindFlagAsync(string key, bool tracking, CancellationToken cancellationToken);

    void AddFlag(FeatureFlag flag);

    Task<IReadOnlyList<FinanceReportRow>> FinanceReportAsync(DateOnly? from, DateOnly? to, string? state, string? city, Guid? clinicId, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
