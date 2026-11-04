namespace ProDoc.Application.Abstractions;

public interface IOtpCodeProtector
{
    string Hash(string code);

    bool Verify(string code, string hash);
}
