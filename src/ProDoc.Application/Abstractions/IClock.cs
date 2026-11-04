namespace ProDoc.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
