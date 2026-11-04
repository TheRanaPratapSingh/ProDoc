using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Domain.Common;

namespace ProDoc.Infrastructure.Providers;

public sealed class ProviderFileOptions
{
    public const string SectionName = "ProviderFiles";

    public string? Root { get; set; }
}

public sealed partial class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalDocumentStorage(IWebHostEnvironment environment, IOptions<ProviderFileOptions> options)
    {
        _root = string.IsNullOrWhiteSpace(options.Value.Root)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "provider-documents")
            : Path.GetFullPath(options.Value.Root);
        if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
        {
            var webRoot = Path.GetFullPath(environment.WebRootPath);
            var root = Path.GetFullPath(_root);
            if (root.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Provider files must not be stored under the public web root.");
            }
        }

        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var key = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = SafePath(key);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
        return new StoredFile(key, file.Length);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = SafePath(storageKey);
        if (!File.Exists(path))
        {
            throw DomainException.NotFound("document_not_found", "That document was not found.");
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private string SafePath(string storageKey)
    {
        if (!StorageKeyPattern().IsMatch(storageKey))
        {
            throw DomainException.NotFound("document_not_found", "That document was not found.");
        }

        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        var root = Path.GetFullPath(_root);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.NotFound("document_not_found", "That document was not found.");
        }

        return path;
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.(pdf|jpg|png|webp)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StorageKeyPattern();
}
