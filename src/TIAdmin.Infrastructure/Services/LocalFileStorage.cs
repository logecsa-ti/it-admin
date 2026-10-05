namespace TIAdmin.Infrastructure.Services;

using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;

/// <summary>
/// Almacenamiento en disco (Storage:RootPath). Toda ruta relativa se resuelve dentro de la raiz:
/// una ruta que intente salir de ella ("..", absoluta) se rechaza.
/// </summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string root = Path.GetFullPath(options.Value.RootPath);

    public async Task<StoredFile> SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var sha = SHA256.Create();
        long size;
        await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        await using (var hashing = new CryptoStream(file, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashing, cancellationToken);
            await hashing.FlushFinalBlockAsync(cancellationToken);
            size = file.Length;
        }

        return new StoredFile(relativePath, size, Convert.ToHexString(sha.Hash!).ToLowerInvariant());
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> CanWriteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".health-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(probe, "ok", cancellationToken);
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("La ruta de almacenamiento debe ser relativa.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La ruta de almacenamiento sale de la raiz.", nameof(relativePath));
        }

        return fullPath;
    }
}
