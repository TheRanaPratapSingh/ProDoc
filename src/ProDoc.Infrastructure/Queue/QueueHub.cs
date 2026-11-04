using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProDoc.Application.Queue;
using ProDoc.Domain.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProDoc.Infrastructure.Queue;

[Authorize]
public sealed class QueueHub(IClinicAccess access, IQueueStore queue) : Hub
{
    public async Task SubscribeDoctorQueue(Guid clinicId, Guid branchId, Guid doctorId, string operatingDate)
    {
        var userId = UserId();
        await access.EnsureDoctorQueueAsync(userId, doctorId, clinicId, branchId, Context.ConnectionAborted);
        if (!DateOnly.TryParse(operatingDate, out var date))
        {
            throw DomainException.Validation("date_invalid", "Choose a valid operating date.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"queue:doctor:{doctorId}:{branchId}:{date:yyyy-MM-dd}");
    }

    public async Task SubscribeBranchQueue(Guid clinicId, Guid branchId)
    {
        var userId = UserId();
        await access.EnsureQueueAccessAsync(userId, clinicId, branchId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"queue:branch:{branchId}");
    }

    public async Task SubscribeMyEntry(Guid entryId)
    {
        var userId = UserId();
        var entry = await queue.FindEntryAsync(entryId, tracking: false, Context.ConnectionAborted)
            ?? throw DomainException.NotFound("queue_entry_not_found", "That queue entry was not found.");
        if (entry.AccountUserId != userId)
        {
            throw DomainException.NotFound("queue_entry_not_found", "That queue entry was not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"queue:entry:{entryId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    private Guid UserId()
    {
        var subject = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            throw DomainException.Unauthorized("unauthorized", "Sign in required.");
        }

        return userId;
    }
}
