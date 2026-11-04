using ProDoc.Domain.Governance;
using ProDoc.Domain.Mobile;

namespace ProDoc.Application.Mobile;

public interface IMobileStore
{
    Task<PushDevice?> FindByTokenAsync(string token, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<PushDevice>> ListActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(PushDevice device);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPushNotificationSender
{
    Task SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
}
