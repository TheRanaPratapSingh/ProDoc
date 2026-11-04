using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProDoc.Api.Auth;
using ProDoc.Application.Assistant;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("assistant")]
[Route("api/v1/assistant")]
public sealed class AssistantController(AssistantService assistant) : ControllerBase
{
    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpGet("consent")]
    public Task<AssistantConsentDetails> GetConsent([FromQuery] string? language, CancellationToken cancellationToken) =>
        assistant.GetConsentAsync(User.GetUserId(), language, cancellationToken);

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpPut("consent")]
    public Task<AssistantConsentDetails> SetConsent([FromBody] AssistantConsentRequest request, CancellationToken cancellationToken) =>
        assistant.SetRecordsConsentAsync(User.GetUserId(), request.AllowRecordsAccess, request.Language, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpGet("conversations")]
    public Task<IReadOnlyList<AssistantConversationSummary>> List(CancellationToken cancellationToken) =>
        assistant.ListConversationsAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpPost("conversations")]
    public Task<AssistantConversationDetails> Start([FromBody] StartAssistantConversationRequest request, CancellationToken cancellationToken) =>
        assistant.StartConversationAsync(User.GetUserId(), request.Language, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpGet("conversations/{conversationId:guid}")]
    public Task<AssistantConversationDetails> Get(Guid conversationId, CancellationToken cancellationToken) =>
        assistant.GetConversationAsync(User.GetUserId(), conversationId, cancellationToken);

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> Delete(Guid conversationId, CancellationToken cancellationToken)
    {
        await assistant.DeleteConversationAsync(User.GetUserId(), conversationId, Meta(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public Task<AssistantTurnResult> Send(Guid conversationId, [FromBody] AssistantMessageRequest request, CancellationToken cancellationToken) =>
        assistant.SendMessageAsync(
            User.GetUserId(),
            conversationId,
            request.Content,
            request.City,
            request.Latitude,
            request.Longitude,
            Meta(),
            cancellationToken);

    [HasPermission(PermissionCodes.AssistantUse)]
    [HttpPost("conversations/{conversationId:guid}/actions/{actionId:guid}/confirm")]
    public Task<ConfirmAssistantActionResult> Confirm(Guid conversationId, Guid actionId, CancellationToken cancellationToken) =>
        assistant.ConfirmPendingActionAsync(User.GetUserId(), conversationId, actionId, Meta(), cancellationToken);

    [HasPermission(PermissionCodes.DoctorsSearch)]
    [HttpGet("recommendations")]
    public Task<IReadOnlyList<AssistantDoctorRecommendation>> Recommend(
        [FromQuery] string? q,
        [FromQuery] string? city,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        CancellationToken cancellationToken) =>
        assistant.RecommendAsync(User.GetUserId(), q, city, latitude, longitude, cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record AssistantConsentRequest(bool AllowRecordsAccess, string? Language);

public sealed record StartAssistantConversationRequest(string? Language);

public sealed record AssistantMessageRequest(
    string Content,
    string? City,
    double? Latitude,
    double? Longitude);
