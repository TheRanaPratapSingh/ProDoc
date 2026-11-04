namespace ProDoc.Domain.Access;

public sealed class Permission
{
    private Permission()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public static Permission Create(string code, string description)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A permission code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A permission description is required.", nameof(description));
        }

        return new Permission
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Description = description.Trim()
        };
    }
}
