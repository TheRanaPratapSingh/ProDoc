namespace ProDoc.Application.Appointments;

public sealed class AppointmentShareOptions
{
    public const string SectionName = "AppointmentSharing";

    /// <summary>Public HTTPS base used in shared messages, e.g. https://prodoc.app/share/</summary>
    public string PublicBaseUrl { get; set; } = "https://prodoc.app/share/";

    /// <summary>Default lifetime in hours for newly minted share links.</summary>
    public int DefaultLifetimeHours { get; set; } = 168;
}
