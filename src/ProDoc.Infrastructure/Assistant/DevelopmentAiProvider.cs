using System.Text;
using Microsoft.Extensions.Options;
using ProDoc.Application.Assistant;

namespace ProDoc.Infrastructure.Assistant;

/// <summary>
/// Grounded composer used when no external LLM key is configured.
/// It formats tool/context facts only and never invents providers or clinical advice.
/// </summary>
public sealed class DevelopmentAiProvider(IOptions<AiOptions> options) : IAiProvider
{
    public string ProviderName => "Development";

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = new StringBuilder();
        foreach (var notice in request.SafetyNotices.Take(1))
        {
            body.AppendLine(notice);
            body.AppendLine();
        }

        var context = request.GroundedContext?.Trim() ?? string.Empty;
        if (context.Contains("Emergency pathway", StringComparison.OrdinalIgnoreCase))
        {
            body.AppendLine(request.Language == "hi"
                ? "यह आपातकालीन स्थिति लगती है। तुरंत आपातकालीन सेवा से संपर्क करें।"
                : "This looks like an emergency. Contact emergency services immediately.");
        }
        else if (context.Contains("Prompt-injection", StringComparison.OrdinalIgnoreCase))
        {
            body.AppendLine(request.Language == "hi"
                ? "मैं सुरक्षा नियमों को अनदेखा नहीं कर सकता।"
                : "I cannot ignore safety rules or system instructions.");
        }
        else if (context.Contains("Records access blocked", StringComparison.OrdinalIgnoreCase))
        {
            body.AppendLine(request.Language == "hi"
                ? "मेडिकल रिकॉर्ड देखने से पहले AI सहमति दें। सहमति के बिना मैं आपके रिकॉर्ड नहीं पढ़ूँगा।"
                : "Grant AI records consent before I can read your medical records. Without consent I will not access them.");
        }
        else if (context.Contains("Refuse autonomous clinical", StringComparison.OrdinalIgnoreCase)
                 || context.Contains("Do not change medicines", StringComparison.OrdinalIgnoreCase))
        {
            body.AppendLine(request.Language == "hi"
                ? "मैं निदान, प्रिस्क्रिप्शन या दवा की खुराक नहीं बता सकता। यह जानकारी डॉक्टर से चर्चा करें। नीचे केवल अधिकृत रिकॉर्ड तथ्य हैं, यदि उपलब्ध हों।"
                : "I cannot diagnose, prescribe, or recommend medicine doses. Please discuss clinical decisions with a qualified doctor. Below are authorized record facts only when available.");
            AppendFacts(body, context);
        }
        else if (context.Contains("Verified doctor recommendations", StringComparison.OrdinalIgnoreCase))
        {
            body.AppendLine(request.Language == "hi"
                ? "ये सुझाव केवल सत्यापित ProDoc डॉक्टरों और उपलब्ध स्लॉट पर आधारित हैं। रेटिंग तब तक नहीं दिखाई जाती जब तक पर्याप्त वास्तविक समीक्षाएँ न हों।"
                : "These suggestions are based only on verified ProDoc doctors and availability. Ratings are omitted until enough authentic reviews exist.");
            AppendBulletFacts(body, context);
            if (context.Contains("Proposed booking requires explicit confirmation", StringComparison.OrdinalIgnoreCase))
            {
                body.AppendLine();
                body.AppendLine(request.Language == "hi"
                    ? "अपॉइंटमेंट तभी बनेगा जब आप स्पष्ट रूप से पुष्टि करें।"
                    : "Nothing is booked until you explicitly confirm the proposed visit.");
            }
        }
        else if (context.Contains("Cancellation and reschedule", StringComparison.OrdinalIgnoreCase))
        {
            body.AppendLine(request.Language == "hi"
                ? "रद्द/रीशेड्यूल करने के लिए Visits में जाकर पुष्टि करें। सहायक स्वयं अपॉइंटमेंट नहीं बदलता।"
                : "Use Visits to cancel or reschedule with confirmation. The assistant will not change appointments on its own.");
            AppendBulletFacts(body, context);
        }
        else
        {
            body.AppendLine(request.Language == "hi"
                ? "मैं डॉक्टर खोजने, बुकिंग समझाने और सामान्य स्वास्थ्य शिक्षा में मदद कर सकता हूँ। यह चिकित्सकीय सलाह नहीं है।"
                : "I can help you find doctors, understand booking, and share general health education. This is not personal medical advice.");
            AppendFacts(body, context);
        }

        var content = TrimToLimit(body.ToString().Trim(), options.Value.MaxTokensPerReply);
        var promptTokens = EstimateTokens(request.GroundedContext) + request.Messages.Sum(item => EstimateTokens(item.Content));
        var completionTokens = EstimateTokens(content);
        return Task.FromResult(new AiCompletionResult(content, ProviderName, "grounded-templates-v1", promptTokens, completionTokens, UsedFallback: false));
    }

    private static void AppendFacts(StringBuilder body, string context)
    {
        foreach (var line in context.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("FACT ", StringComparison.OrdinalIgnoreCase))
            {
                body.AppendLine("• " + line["FACT ".Length..]);
            }
        }
    }

    private static void AppendBulletFacts(StringBuilder body, string context)
    {
        foreach (var line in context.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("Visit ", StringComparison.Ordinal))
            {
                body.AppendLine("• " + line.TrimStart('-', ' '));
            }
        }
    }

    private static int EstimateTokens(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, text.Length / 4);

    private static string TrimToLimit(string content, int maxTokens)
    {
        var maxChars = Math.Max(200, maxTokens * 4);
        return content.Length <= maxChars ? content : content[..maxChars].TrimEnd() + "…";
    }
}
