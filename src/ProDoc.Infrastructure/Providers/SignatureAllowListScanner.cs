using ProDoc.Application.Abstractions;

namespace ProDoc.Infrastructure.Providers;

/// <summary>
/// Checks file signatures and blocks executable extensions. This is the integration point for a malware scanner.
/// It does not claim a file is free of malware.
/// </summary>
public sealed class SignatureAllowListScanner : IUploadScanner
{
    public const string Engine = "signature-allow-list";

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".js", ".msi", ".ps1", ".scr", ".vbs"
    };

    public async Task<UploadScan> ScanAsync(
        Stream content,
        string fileName,
        string contentType,
        bool imagesOnly,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (BlockedExtensions.Contains(extension))
        {
            return Reject("This file type is not allowed.");
        }

        var header = new byte[16];
        var read = await content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var detected = Detect(header.AsSpan(0, read));
        if (detected is null)
        {
            return Reject("Upload a PDF, JPEG, PNG, or WebP file.");
        }

        if (imagesOnly && detected == "application/pdf")
        {
            return Reject("Upload a JPEG, PNG, or WebP image.");
        }

        if (!string.IsNullOrWhiteSpace(contentType)
            && !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && !contentType.Equals(detected, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("The file contents do not match its type.");
        }

        return new UploadScan(
            true,
            Engine,
            "Malware scanning is not configured. Only the file signature and size were checked.",
            detected);
    }

    private static UploadScan Reject(string detail) => new(false, Engine, detail, null);

    private static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
        {
            return "application/pdf";
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (header.Length >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (header.Length >= 12
            && header[0] == (byte)'R'
            && header[1] == (byte)'I'
            && header[2] == (byte)'F'
            && header[3] == (byte)'F'
            && header[8] == (byte)'W'
            && header[9] == (byte)'E'
            && header[10] == (byte)'B'
            && header[11] == (byte)'P')
        {
            return "image/webp";
        }

        return null;
    }
}
