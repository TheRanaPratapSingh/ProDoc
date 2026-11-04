namespace ProDoc.Domain.Providers;

public sealed class Specialization
{
    private Specialization()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static Specialization Create(Guid id, string code, string name)
    {
        return new Specialization
        {
            Id = id,
            Code = code.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            IsActive = true
        };
    }
}

public static class SpecializationCatalog
{
    public static readonly IReadOnlyList<Specialization> All =
    [
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000001", "general-medicine", "General Medicine"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000002", "cardiology", "Cardiology"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000003", "dermatology", "Dermatology"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000004", "pediatrics", "Pediatrics"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000005", "orthopedics", "Orthopedics"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000006", "gynecology", "Gynecology"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000007", "ent", "ENT"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000008", "ophthalmology", "Ophthalmology"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-000000000009", "psychiatry", "Psychiatry"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-00000000000a", "dentistry", "Dentistry"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-00000000000b", "ayurveda", "Ayurveda"),
        Item("b1d0c2a0-5c1e-4a11-9c1a-00000000000c", "neurology", "Neurology")
    ];

    private static Specialization Item(string id, string code, string name) =>
        Specialization.Create(Guid.Parse(id), code, name);
}
