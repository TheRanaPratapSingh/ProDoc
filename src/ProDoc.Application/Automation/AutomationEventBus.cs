using System.Text.Json;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Partners;
using ProDoc.Domain.Automation;

namespace ProDoc.Application.Automation;

public sealed class AutomationEventBus(
    IAutomationStore store,
    IPartnerWebhookPublisher partnerWebhooks,
    IClock clock) : IAutomationEventBus
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(
        string eventType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken,
        Guid? organizationId = null,
        Guid? subjectId = null,
        Guid? userId = null,
        DateTimeOffset? availableAtUtc = null)
    {
        if (await store.OutboxExistsAsync(idempotencyKey, cancellationToken))
        {
            return;
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        store.AddOutbox(AutomationOutboxEvent.Create(
            eventType,
            json,
            idempotencyKey,
            clock.UtcNow,
            organizationId,
            subjectId,
            userId,
            availableAtUtc));
        await store.SaveChangesAsync(cancellationToken);
        await partnerWebhooks.PublishAsync(eventType, payload, idempotencyKey, cancellationToken, organizationId);
    }
}
