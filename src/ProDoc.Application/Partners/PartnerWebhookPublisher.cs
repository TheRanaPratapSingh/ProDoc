using System.Text.Json;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Partners;

namespace ProDoc.Application.Partners;

public sealed class PartnerWebhookPublisher(IPartnerStore store, IClock clock) : IPartnerWebhookPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> EventMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [AutomationTriggerTypes.AppointmentCreated] = PartnerWebhookEvents.AppointmentCreated,
        [AutomationTriggerTypes.AppointmentConfirmed] = PartnerWebhookEvents.AppointmentStatusChanged,
        [AutomationTriggerTypes.AppointmentCancelled] = PartnerWebhookEvents.AppointmentCancelled,
        [AutomationTriggerTypes.PaymentCompleted] = PartnerWebhookEvents.PaymentStatusChanged,
        [AutomationTriggerTypes.PaymentFailed] = PartnerWebhookEvents.PaymentStatusChanged,
        [AutomationTriggerTypes.PrescriptionFinalized] = PartnerWebhookEvents.PrescriptionFinalized
    };

    public async Task PublishAsync(
        string eventType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken,
        Guid? organizationId = null)
    {
        if (!EventMap.TryGetValue(eventType, out var partnerEvent))
        {
            return;
        }

        // Prescription exchange is opt-in via endpoint subscription; still fan-out only to apps that subscribed.
        var endpoints = await store.ListActiveEndpointsForEventAsync(partnerEvent, cancellationToken);
        if (endpoints.Count == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new
        {
            eventType = partnerEvent,
            occurredAtUtc = clock.UtcNow,
            organizationId,
            data = payload
        }, JsonOptions);

        foreach (var endpoint in endpoints)
        {
            var key = $"{partnerEvent}:{idempotencyKey}";
            if (key.Length > 160)
            {
                key = key[..160];
            }

            if (await store.DeliveryExistsAsync(key, cancellationToken))
            {
                continue;
            }

            store.AddDelivery(PartnerWebhookDelivery.Create(
                endpoint.Id,
                endpoint.PartnerAppId,
                partnerEvent,
                key,
                json,
                clock.UtcNow));
        }

        await store.SaveChangesAsync(cancellationToken);
    }
}
