using Microsoft.Extensions.Logging;
using ProDoc.Application.Automation;

namespace ProDoc.Infrastructure.Automation;

public sealed class DevelopmentAutomationMessenger(ILogger<DevelopmentAutomationMessenger> logger) : IAutomationMessenger
{
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("DEV EMAIL to={To} subject={Subject} body={Body}", to, subject, body);
        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneE164, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("DEV SMS to={Phone} body={Body}", phoneE164, body);
        return Task.CompletedTask;
    }
}
