using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Automation;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class AutomationController(IAutomationService automation) : ControllerBase
{
    [HttpGet("automation/catalog")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public AutomationCatalog Catalog() => automation.Catalog();

    [HttpGet("automation/workflows")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<IReadOnlyList<AutomationWorkflowDetails>> Workflows(
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken) =>
        automation.ListWorkflowsAsync(User.GetUserId(), organizationId, cancellationToken);

    [HttpPost("automation/workflows")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<AutomationWorkflowDetails> Create(CreateWorkflowRequest request, CancellationToken cancellationToken) =>
        automation.CreateWorkflowAsync(
            User.GetUserId(),
            request.Name,
            request.TriggerType,
            request.Conditions,
            request.Actions,
            request.DelayMinutes,
            request.OrganizationId,
            Meta(),
            cancellationToken);

    [HttpPut("automation/workflows/{workflowId:guid}")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<AutomationWorkflowDetails> Update(Guid workflowId, UpdateWorkflowRequest request, CancellationToken cancellationToken) =>
        automation.UpdateWorkflowAsync(
            User.GetUserId(),
            workflowId,
            request.Name,
            request.Conditions,
            request.Actions,
            request.DelayMinutes,
            Meta(),
            cancellationToken);

    [HttpPost("automation/workflows/{workflowId:guid}/enabled")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<AutomationWorkflowDetails> SetEnabled(Guid workflowId, SetEnabledRequest request, CancellationToken cancellationToken) =>
        automation.SetEnabledAsync(User.GetUserId(), workflowId, request.Enabled, Meta(), cancellationToken);

    [HttpGet("automation/runs")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<IReadOnlyList<AutomationWorkflowRunDetails>> Runs([FromQuery] Guid? workflowId, CancellationToken cancellationToken) =>
        automation.ListRunsAsync(workflowId, cancellationToken);

    [HttpGet("automation/outbox")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<IReadOnlyList<AutomationOutboxDetails>> Outbox([FromQuery] string? status, CancellationToken cancellationToken) =>
        automation.ListOutboxAsync(status, cancellationToken);

    [HttpGet("automation/analytics")]
    [HasPermission(PermissionCodes.AutomationManage)]
    public Task<AutomationAnalytics> Analytics(CancellationToken cancellationToken) =>
        automation.AnalyticsAsync(cancellationToken);

    [HttpGet("account/notification-preferences")]
    public Task<NotificationPreferenceDetails> GetPreferences(CancellationToken cancellationToken) =>
        automation.GetPreferencesAsync(User.GetUserId(), cancellationToken);

    [HttpPut("account/notification-preferences")]
    public Task<NotificationPreferenceDetails> PutPreferences(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken) =>
        automation.UpdatePreferencesAsync(
            User.GetUserId(),
            request.InAppEnabled,
            request.EmailEnabled,
            request.SmsEnabled,
            request.PushEnabled,
            request.MarketingEnabled,
            request.AppointmentRemindersEnabled,
            request.QueueUpdatesEnabled,
            Meta(),
            cancellationToken);

    private RequestMetadata Meta() => new(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext));
}

public sealed record CreateWorkflowRequest(
    string Name,
    string TriggerType,
    IReadOnlyList<AutomationConditionDto> Conditions,
    IReadOnlyList<AutomationActionDto> Actions,
    int DelayMinutes = 0,
    Guid? OrganizationId = null);

public sealed record UpdateWorkflowRequest(
    string Name,
    IReadOnlyList<AutomationConditionDto> Conditions,
    IReadOnlyList<AutomationActionDto> Actions,
    int DelayMinutes = 0);

public sealed record SetEnabledRequest(bool Enabled);

public sealed record UpdateNotificationPreferencesRequest(
    bool InAppEnabled,
    bool EmailEnabled,
    bool SmsEnabled,
    bool PushEnabled,
    bool MarketingEnabled,
    bool AppointmentRemindersEnabled,
    bool QueueUpdatesEnabled);
