using System.Text.RegularExpressions;
using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;

namespace ProDoc.Domain.Patients;

public sealed partial class Patient
{
    public const int MaxNameLength = 80;

    public const int MinAgeYears = 1;

    public const int MaxAgeYears = 120;

    private Patient()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string? FullName { get; private set; }

    /// <summary>Completed years of age. Null until the patient provides it.</summary>
    public int? AgeYears { get; private set; }

    /// <summary>Optional: male | female | other.</summary>
    public string? Gender { get; private set; }

    /// <summary>
    /// Persisted completion flag. Existing named patients are grandfathered via migration;
    /// new patients must submit Name + Age before Patient Home.
    /// </summary>
    public bool ProfileCompleted { get; private set; }

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
            ProfileCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateProfile(
        string? fullName,
        string preferredLanguage,
        DateTimeOffset now,
        int? ageYears = null,
        string? gender = null)
    {
        EnsureActive();
        FullName = NormalizeName(fullName);
        if (!Languages.IsSupported(preferredLanguage))
        {
            throw DomainException.Validation("language_invalid", "Choose English or Hindi.");
        }

        PreferredLanguage = preferredLanguage;
        if (ageYears is not null)
        {
            AgeYears = RequireAge(ageYears);
        }

        if (gender is not null)
        {
            Gender = NormalizeOptionalGender(gender);
        }

        RefreshCompletion();
        UpdatedAtUtc = now;
    }

    /// <summary>First-time mandatory Name + Age (Gender optional).</summary>
    public void CompleteBasicProfile(
        string fullName,
        int ageYears,
        string? gender,
        string preferredLanguage,
        DateTimeOffset now)
    {
        EnsureActive();
        FullName = NormalizeName(fullName)
            ?? throw DomainException.Validation("name_mandatory", "Name is mandatory.");
        AgeYears = RequireAge(ageYears);
        Gender = NormalizeOptionalGender(gender);
        if (!Languages.IsSupported(preferredLanguage))
        {
            throw DomainException.Validation("language_invalid", "Choose English or Hindi.");
        }

        PreferredLanguage = preferredLanguage;
        ProfileCompleted = true;
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

    /// <summary>
    /// Scrubs profile PII while retaining the patient row for clinical/appointment/audit integrity.
    /// </summary>
    public void AnonymizeForPermanentDeletion(DateTimeOffset now)
    {
        FullName = "Deleted User";
        AgeYears = null;
        Gender = null;
        ProfileCompleted = false;
        DeletionRequestedAtUtc ??= now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Clears a pending deletion so a Super-Admin staff invite can reactivate the account for clinical roles.
    /// </summary>
    public void ClearDeletionRequest(DateTimeOffset now)
    {
        if (DeletionRequestedAtUtc is null)
        {
            return;
        }

        DeletionRequestedAtUtc = null;
        UpdatedAtUtc = now;
    }

    private void RefreshCompletion()
    {
        if (!string.IsNullOrWhiteSpace(FullName) && AgeYears is not null)
        {
            ProfileCompleted = true;
        }
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

    private static int RequireAge(int? ageYears)
    {
        if (ageYears is null)
        {
            throw DomainException.Validation("age_mandatory", "Age is mandatory.");
        }

        if (ageYears is < MinAgeYears or > MaxAgeYears)
        {
            throw DomainException.Validation("age_invalid", "Please enter a valid age.");
        }

        return ageYears.Value;
    }

    private static string? NormalizeOptionalGender(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
        {
            return null;
        }

        var normalized = gender.Trim().ToLowerInvariant();
        if (normalized is "male" or "female" or "other")
        {
            return normalized;
        }

        // Reuse provider vocabulary for undisclosed / legacy values when provided.
        return ProviderText.Gender(normalized);
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
