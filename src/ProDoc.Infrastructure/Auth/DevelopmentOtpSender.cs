using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Identity;

namespace ProDoc.Infrastructure.Auth;

public sealed class DevelopmentOtpSender(ILogger<DevelopmentOtpSender> logger) : IOtpSender
{
    public Task SendSignInCodeAsync(string phoneE164, string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        logger.LogInformation(
            "Development OTP accepted for {MaskedPhone}. The code is returned to the client and is not written to logs.",
            PhoneNumber.ParseIndianMobile(phoneE164).Masked);
        return Task.CompletedTask;
    }
}
