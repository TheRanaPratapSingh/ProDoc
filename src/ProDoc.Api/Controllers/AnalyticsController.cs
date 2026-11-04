using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Analytics;
using ProDoc.Application.Providers;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/analytics")]
public sealed class AnalyticsController(IAnalyticsService analytics) : ControllerBase
{
    [HasPermission(PermissionCodes.AnalyticsRead)]
    [HttpGet("executive")]
    public Task<ExecutiveAnalytics> Executive(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolveRange(from, to);
        return analytics.GetExecutiveAsync(start, end, cancellationToken);
    }

    [HasPermission(PermissionCodes.AnalyticsRead)]
    [HttpGet("operational")]
    public Task<OperationalAnalytics> Operational(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolveRange(from, to);
        return analytics.GetOperationalAsync(start, end, cancellationToken);
    }

    [HasPermission(PermissionCodes.AnalyticsRead)]
    [HttpGet("predictive")]
    public Task<PredictiveInsights> Predictive(CancellationToken cancellationToken) =>
        analytics.GetPredictiveAsync(cancellationToken);

    [HasPermission(PermissionCodes.AnalyticsExport)]
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string reportType = "executive",
        [FromQuery] string format = "csv",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveRange(from, to);
        var result = await analytics.ExportAsync(
            reportType,
            format,
            start,
            end,
            User.GetUserId(),
            new RequestMetadata(ClientContext.IpAddress(HttpContext), ClientContext.UserAgent(HttpContext)),
            cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    private static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        return (start, end);
    }
}
