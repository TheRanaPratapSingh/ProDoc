using Microsoft.Extensions.Hosting;
using ProDoc.Application.Support;

namespace ProDoc.Infrastructure.Support;

public sealed class LocalSupportFileStore(IHostEnvironment environment) : ISupportFileStore
{
    public async Task<string> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        var key = $"support/{ticketId:N}/{Guid.NewGuid():N}-{safeName}";
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "support-attachments", key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "support-attachments", storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Support attachment missing.", path);
        }

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}
