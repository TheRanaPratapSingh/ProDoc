namespace ProDoc.Application.Abstractions;

public readonly record struct IssuedRefreshToken(string Plaintext, string Hash);

public interface IRefreshTokenFactory
{
    IssuedRefreshToken Create();

    string Hash(string plaintext);
}
