namespace ProDoc.Application.Abstractions;

public interface IOtpSender
{
    Task SendSignInCodeAsync(string phoneE164, string code, CancellationToken cancellationToken);
}
