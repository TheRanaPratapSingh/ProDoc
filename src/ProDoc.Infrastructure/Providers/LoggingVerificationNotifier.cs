using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Providers;

public sealed class LoggingVerificationNotifier(ILogger<LoggingVerificationNotifier> logger) : IVerificationNotifier
{
    public void ApplicationSubmitted(string kind, string reference)
    {
        logger.LogInformation("Verification application submitted. Kind {Kind}, reference {Reference}", kind, reference);
    }
}
