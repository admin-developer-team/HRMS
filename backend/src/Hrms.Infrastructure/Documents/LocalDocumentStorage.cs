using Hrms.Application;
using Microsoft.Extensions.Configuration;

namespace Hrms.Infrastructure.Documents;

public sealed class LocalDocumentStorage(IConfiguration configuration) : IDocumentStorage
{
    private readonly string root = Path.GetFullPath(configuration["Documents:StoragePath"]
        ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "documents"));

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(output, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (!File.Exists(path)) throw new KeyNotFoundException("The document file is unavailable.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Document storage key is invalid.");
        return path;
    }
}
