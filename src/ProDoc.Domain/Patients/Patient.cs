using System.Text.RegularExpressions;
using ProDoc.Domain.Common;

namespace ProDoc.Domain.Patients;

public sealed partial class Patient
{
    public const int MaxNameLength = 80;

    private Patient()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string? FullName { get; private set; }

    public string PreferredLanguage { get; private set; } = Languages.English;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletionRequestedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static Patient Register(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        return new Patient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PreferredLanguage = Languages.English,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateProfile(string? fullName, string preferredLanguage, DateTimeOffset now)
    {
        EnsureActive();
        FullName = NormalizeName(fullName);
        if (!Languages.IsSupported(preferredLanguage))
        {
            throw DomainException.Validation("language_invalid", "Choose English or Hindi.");
        }

        PreferredLanguage = preferredLanguage;
        UpdatedAtUtc = now;
    }

    public void RequestDeletion(DateTimeOffset now)
    {
        if (DeletionRequestedAtUtc is not null)
        {
            return;
        }

        DeletionRequestedAtUtc = now;
        UpdatedAtUtc = now;
    }

    private void EnsureActive()
    {
        if (DeletionRequestedAtUtc is not null)
        {
            throw DomainException.Validation(
                "account_deletion_pending",
                "This account is pending deletion and cannot be changed.");
        }
    }

    private static string? NormalizeName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var trimmed = string.Join(' ', fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (trimmed.Length > MaxNameLength || !NamePattern().IsMatch(trimmed))
        {
            throw DomainException.Validation(
                "name_invalid",
                "Enter a name using letters, spaces, apostrophes, or hyphens.");
        }

        return trimmed;
    }

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M} .'-]{1,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
