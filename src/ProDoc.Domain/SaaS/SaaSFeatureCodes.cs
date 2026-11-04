namespace ProDoc.Domain.SaaS;

public static class SaaSFeatureCodes
{
    public const string MaxClinics = "limits.clinics";
    public const string MaxBranches = "limits.branches";
    public const string MaxDoctors = "limits.doctors";
    public const string MaxStaff = "limits.staff";
    public const string MaxStorageMb = "limits.storage_mb";
    public const string WhiteLabel = "feature.white_label";
    public const string CustomDomain = "feature.custom_domain";
    public const string AdvancedAnalytics = "feature.advanced_analytics";
    public const string Integrations = "feature.integrations";
    public const string AiAssistant = "feature.ai_assistant";

    public static IReadOnlyList<string> Catalog { get; } =
    [
        MaxClinics,
        MaxBranches,
        MaxDoctors,
        MaxStaff,
        MaxStorageMb,
        WhiteLabel,
        CustomDomain,
        AdvancedAnalytics,
        Integrations,
        AiAssistant
    ];
}
