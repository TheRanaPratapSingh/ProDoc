using ProDoc.Domain.Common;

namespace ProDoc.Domain.Assistant;

public enum AssistantMessageRole
{
    User = 1,
    Assistant = 2,
    System = 3,
    Tool = 4
}

public enum AssistantPendingActionType
{
    None = 0,
    BookAppointment = 1,
    CancelAppointment = 2,
    RescheduleAppointment = 3
}

public static class AssistantPromptVersions
{
    public const string Current = "prodoc-assistant-v1";
}

public static class AssistantSafetyCopy
{
    public const string DisclaimerEn =
        "ProDoc Assistant helps you navigate care. It does not diagnose, prescribe, or replace a qualified doctor.";

    public const string DisclaimerHi =
        "ProDoc सहायक देखभाल खोजने में मदद करता है। यह निदान, दवा या डॉक्टर की सलाह का विकल्प नहीं है।";

    public const string EmergencyEn =
        "If this is an emergency, seek immediate medical help. Call local emergency services or go to the nearest emergency department now.";

    public const string EmergencyHi =
        "यदि यह आपातकाल है, तुरंत चिकित्सा सहायता लें। स्थानीय आपातकालीन सेवाओं पर कॉल करें या नज़दीकी आपातकालीन विभाग जाएँ।";
}

public sealed class AssistantConversation
{
    private readonly List<AssistantMessage> _messages = [];

    private AssistantConversation()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Title { get; private set; } = "New chat";

    public string Language { get; private set; } = "en";

    public string PromptVersion { get; private set; } = AssistantPromptVersions.Current;

    public string? PendingActionJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public IReadOnlyCollection<AssistantMessage> Messages => _messages;

    public static AssistantConversation Start(Guid userId, string language, DateTimeOffset now)
    {
        var lang = NormalizeLanguage(language);
        return new AssistantConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Language = lang,
            PromptVersion = AssistantPromptVersions.Current,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public AssistantMessage AddMessage(AssistantMessageRole role, string content, string? toolName, IReadOnlyList<string>? sources, DateTimeOffset now)
    {
        EnsureActive();
        var message = AssistantMessage.Create(Id, role, content, toolName, sources, now);
        _messages.Add(message);
        UpdatedAtUtc = now;
        if (role == AssistantMessageRole.User && Title == "New chat")
        {
            var trimmed = content.Trim();
            Title = trimmed.Length <= 80 ? trimmed : trimmed[..77] + "...";
        }

        return message;
    }

    public void SetPendingAction(string? json, DateTimeOffset now)
    {
        EnsureActive();
        PendingActionJson = string.IsNullOrWhiteSpace(json) ? null : json;
        UpdatedAtUtc = now;
    }

    public void SoftDelete(DateTimeOffset now)
    {
        DeletedAtUtc = now;
        PendingActionJson = null;
        UpdatedAtUtc = now;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
        {
            throw DomainException.Validation("conversation_deleted", "That conversation was deleted.");
        }
    }

    public static string NormalizeLanguage(string? language)
    {
        var value = (language ?? "en").Trim().ToLowerInvariant();
        return value is "hi" or "en" ? value : "en";
    }
}

public sealed class AssistantMessage
{
    private AssistantMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public AssistantMessageRole Role { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public string? ToolName { get; private set; }

    public string? SourcesJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AssistantMessage Create(
        Guid conversationId,
        AssistantMessageRole role,
        string content,
        string? toolName,
        IReadOnlyList<string>? sources,
        DateTimeOffset now)
    {
        var text = content.Trim();
        if (text.Length is < 1 or > 8000)
        {
            throw DomainException.Validation("assistant_message_invalid", "Enter a message from 1 to 8000 characters.");
        }

        return new AssistantMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = text,
            ToolName = string.IsNullOrWhiteSpace(toolName) ? null : toolName.Trim()[..Math.Min(toolName.Trim().Length, 80)],
            SourcesJson = sources is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(sources) : null,
            CreatedAtUtc = now
        };
    }
}

public sealed class AssistantConsent
{
    private AssistantConsent()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public bool RecordsAccessAllowed { get; private set; }

    public string ConsentVersion { get; private set; } = "v1";

    public DateTimeOffset? RecordsConsentAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AssistantConsent Create(Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RecordsAccessAllowed = false,
            ConsentVersion = "v1",
            UpdatedAtUtc = now
        };

    public void SetRecordsAccess(bool allowed, DateTimeOffset now)
    {
        RecordsAccessAllowed = allowed;
        RecordsConsentAtUtc = allowed ? now : null;
        ConsentVersion = "v1";
        UpdatedAtUtc = now;
    }
}

public sealed class AssistantUsageDaily
{
    private AssistantUsageDaily()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateOnly UsageDate { get; private set; }

    public int RequestCount { get; private set; }

    public int PromptTokens { get; private set; }

    public int CompletionTokens { get; private set; }

    public static AssistantUsageDaily Create(Guid userId, DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UsageDate = date
        };

    public void Add(int promptTokens, int completionTokens)
    {
        RequestCount += 1;
        PromptTokens += Math.Max(0, promptTokens);
        CompletionTokens += Math.Max(0, completionTokens);
    }
}
