using System.Text.RegularExpressions;

namespace ProDoc.Application.Assistant;

public enum AssistantIntent
{
    Emergency,
    PromptInjection,
    RecommendDoctor,
    BookAppointment,
    CancelAppointment,
    RescheduleAppointment,
    MedicalRecords,
    HealthEducation,
    NavigationHelp,
    General
}

public static partial class AssistantSafety
{
    private static readonly string[] EmergencyTerms =
    [
        "chest pain", "can't breathe", "cannot breathe", "difficulty breathing", "severe bleeding",
        "unconscious", "suicide", "kill myself", "overdose", "stroke", "heart attack", "seizure",
        "छाती में दर्द", "सांस नहीं", "साँस नहीं", "बहुत खून", "आत्महत्या", "हार्ट अटैक", "स्ट्रोक"
    ];

    private static readonly string[] DiagnosisTerms =
    [
        "diagnose me", "what disease", "do i have", "is it cancer", "prescribe", "dosage",
        "how many mg", "change my medicine", "stop my medicine", "increase dose", "decrease dose",
        "निदान करो", "क्या बीमारी", "दवाई बताओ", "खुराक", "दवा बदलो"
    ];

    private static readonly string[] InjectionTerms =
    [
        "ignore previous", "ignore all instructions", "system prompt", "developer mode",
        "you are now", "jailbreak", "reveal your prompt", "act as unrestricted",
        "override safety", "disregard the rules", "tool call:", "{\"name\":"
    ];

    public static bool IsEmergency(string text) =>
        ContainsAny(text, EmergencyTerms);

    public static bool LooksLikePromptInjection(string text) =>
        ContainsAny(text, InjectionTerms);

    public static bool RequestsClinicalAutonomy(string text) =>
        ContainsAny(text, DiagnosisTerms);

    public static AssistantIntent ClassifyIntent(string text)
    {
        if (IsEmergency(text))
        {
            return AssistantIntent.Emergency;
        }

        if (LooksLikePromptInjection(text))
        {
            return AssistantIntent.PromptInjection;
        }

        var normalized = text.ToLowerInvariant();
        if (RequestsClinicalAutonomy(normalized)
            && !normalized.Contains("explain", StringComparison.Ordinal)
            && !normalized.Contains("what does", StringComparison.Ordinal)
            && !normalized.Contains("my prescription", StringComparison.Ordinal)
            && !normalized.Contains("मेरी दवा", StringComparison.Ordinal)
            && !normalized.Contains("मेरा प्रिस्क्रिप्शन", StringComparison.Ordinal))
        {
            // Clinical autonomy requests still get education/safety reply, not diagnosis tools.
            return AssistantIntent.HealthEducation;
        }

        if (ContainsAny(normalized, ["cancel appointment", "cancel my visit", "अपॉइंटमेंट रद्द"]))
        {
            return AssistantIntent.CancelAppointment;
        }

        if (ContainsAny(normalized, ["reschedule", "change appointment", "अपॉइंटमेंट बदल"]))
        {
            return AssistantIntent.RescheduleAppointment;
        }

        if (ContainsAny(normalized, ["book", "appointment", "slot", "available", "मुलाकात", "अपॉइंटमेंट", "बुक"]))
        {
            return AssistantIntent.BookAppointment;
        }

        if (ContainsAny(normalized, ["recommend", "find a doctor", "find doctor", "near me", "specialist", "डॉक्टर ढूंढो", "डॉक्टर खोजो", "स्पेशलिस्ट"]))
        {
            return AssistantIntent.RecommendDoctor;
        }

        if (ContainsAny(normalized, ["my record", "my prescription", "medical record", "lab report", "मेरा रिकॉर्ड", "मेरी दवा", "प्रिस्क्रिप्शन"]))
        {
            return AssistantIntent.MedicalRecords;
        }

        if (ContainsAny(normalized, ["how do i book", "how to book", "clinic service", "queue", "check in", "कैसे बुक", "कतार"]))
        {
            return AssistantIntent.NavigationHelp;
        }

        if (ContainsAny(normalized, ["what is", "meaning of", "explain", "education", "क्या होता है", "मतलब"]))
        {
            return AssistantIntent.HealthEducation;
        }

        return AssistantIntent.General;
    }

    public static string? InferSpecializationHint(string text)
    {
        var normalized = text.ToLowerInvariant();
        if (ContainsAny(normalized, ["skin", "rash", "acne", "त्वचा", "खुजली"]))
        {
            return "dermatology";
        }

        if (ContainsAny(normalized, ["child", "fever in baby", "pediatric", "बच्चा", "शिशु"]))
        {
            return "paediatrics";
        }

        if (ContainsAny(normalized, ["heart", "bp", "blood pressure", "cardiac", "दिल", "ब्लड प्रेशर"]))
        {
            return "cardiology";
        }

        if (ContainsAny(normalized, ["bone", "joint", "fracture", "ortho", "हड्डी", "जोड़"]))
        {
            return "orthopaedics";
        }

        if (ContainsAny(normalized, ["pregnant", "pregnancy", "gynae", "गर्भवती", "स्त्री रोग"]))
        {
            return "gynaecology";
        }

        if (ContainsAny(normalized, ["ear", "nose", "throat", "ent", "कान", "नाक", "गला"]))
        {
            return "ent";
        }

        if (ContainsAny(normalized, ["diabetes", "sugar", "thyroid", "डायबिटीज", "शुगर"]))
        {
            return "general-medicine";
        }

        if (ContainsAny(normalized, ["fever", "cold", "cough", "headache", "general", "बुखार", "सर्दी", "खांसी"]))
        {
            return "general-medicine";
        }

        return null;
    }

    public static string SanitizeUserText(string text)
    {
        var cleaned = ControlChars().Replace(text, " ");
        cleaned = MultiSpace().Replace(cleaned, " ").Trim();
        return cleaned.Length > 4000 ? cleaned[..4000] : cleaned;
    }

    private static bool ContainsAny(string text, IEnumerable<string> terms)
    {
        var haystack = text.ToLowerInvariant();
        return terms.Any(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]")]
    private static partial Regex ControlChars();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}
