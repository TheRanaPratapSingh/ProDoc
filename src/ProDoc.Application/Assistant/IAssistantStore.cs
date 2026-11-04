using ProDoc.Domain.Assistant;
using ProDoc.Domain.Governance;

namespace ProDoc.Application.Assistant;

public interface IAssistantStore
{
    Task<AssistantConversation?> FindConversationAsync(Guid conversationId, Guid userId, bool tracking, CancellationToken cancellationToken);

    Task<IReadOnlyList<AssistantConversation>> ListConversationsAsync(Guid userId, int take, CancellationToken cancellationToken);

    void AddConversation(AssistantConversation conversation);

    void AddMessage(AssistantMessage message);

    Task<AssistantConsent?> FindConsentAsync(Guid userId, bool tracking, CancellationToken cancellationToken);

    void AddConsent(AssistantConsent consent);

    Task<AssistantUsageDaily?> FindUsageAsync(Guid userId, DateOnly date, bool tracking, CancellationToken cancellationToken);

    void AddUsage(AssistantUsageDaily usage);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
