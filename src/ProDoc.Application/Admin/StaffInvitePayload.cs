using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProDoc.Application.Admin;

public sealed class StaffInvitePayload
{
    [JsonPropertyName("specializationId")]
    public Guid? SpecializationId { get; set; }

    [JsonPropertyName("yearsOfExperience")]
    public int? YearsOfExperience { get; set; }

    [JsonPropertyName("qualifications")]
    public string? Qualifications { get; set; }

    [JsonPropertyName("registrationNumber")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("clinicIds")]
    public List<Guid>? ClinicIds { get; set; }

    [JsonPropertyName("doctorIds")]
    public List<Guid>? DoctorIds { get; set; }

    [JsonPropertyName("designation")]
    public string? Designation { get; set; }

    public static StaffInvitePayload? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StaffInvitePayload>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}
