namespace ProDoc.Application.Assistant;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Development | OpenAI</summary>
    public string Provider { get; set; } = "Development";

    public string? ApiKey { get; set; }

    public string? BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 45;

    public int MaxRequestsPerUserPerDay { get; set; } = 40;

    public int MaxTokensPerReply { get; set; } = 700;

    public int ConversationRetentionDays { get; set; } = 30;
}

public sealed record AiChatMessage(string Role, string Content);

public sealed record AiCompletionRequest(
    string PromptVersion,
    string Language,
    IReadOnlyList<AiChatMessage> Messages,
    string GroundedContext,
    IReadOnlyList<string> SafetyNotices);

public sealed record AiCompletionResult(
    string Content,
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    bool UsedFallback);

public interface IAiProvider
{
    string ProviderName { get; }

    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken);
}
