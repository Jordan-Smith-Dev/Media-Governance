namespace Umbraco.Community.MediaGovernance.Services;

public interface IEvidenceStore
{
    /// <summary>Stores a file and returns its path relative to the store root. The client filename is used only for its extension.</summary>
    Task<string> SaveAsync(Guid personId, Guid consentId, string fileName, Stream data);

    /// <summary>Opens a stored file for reading, or returns null if it is missing or the path resolves outside the store.</summary>
    Task<Stream?> OpenReadAsync(string path);

    /// <summary>Deletes stored files by relative path. Paths that resolve outside the store root are ignored.</summary>
    Task DeleteAsync(IEnumerable<string> paths);

    /// <summary>Deletes the whole folder holding a person's evidence (also catches orphaned files).</summary>
    Task DeleteForPersonAsync(Guid personId);
}

/// <summary>Which evidence file types are accepted, what their bytes must look like, and the content type to serve them with.</summary>
public static class EvidenceContent
{
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF];

    public static bool IsAllowedExtension(string extension) => ContentTypeFor(extension) is not null;

    /// <summary>The content type for an allowed extension, otherwise null. Never derived from anything the client sent.</summary>
    public static string? ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => null,
    };

    /// <summary>True when the file starts with the signature its extension claims.</summary>
    public static bool MatchesExtension(string extension, ReadOnlySpan<byte> data)
    {
        var signature = extension.ToLowerInvariant() switch
        {
            ".pdf" => Pdf,
            ".png" => Png,
            ".jpg" or ".jpeg" => Jpg,
            _ => null,
        };
        return signature is not null && data.StartsWith(signature);
    }
}

public class EvidenceStore(string rootPath) : IEvidenceStore
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public async Task<string> SaveAsync(Guid personId, Guid consentId, string fileName, Stream data)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty));
        if (string.IsNullOrEmpty(extension) || !EvidenceContent.IsAllowedExtension(extension))
            throw new ArgumentException("Evidence must be a PDF, PNG or JPG file.");

        // Read at most MaxBytes + 1 so oversized uploads are rejected without buffering them in full.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await data.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
                throw new ArgumentException("Evidence files must be 10 MB or smaller.");
        }

        if (!EvidenceContent.MatchesExtension(extension, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            throw new ArgumentException("The file contents do not match its type. Evidence must be a real PDF, PNG or JPG.");

        // Forward slashes always, so stored paths work on any operating system.
        var relative = $"{personId}/{consentId}{extension.ToLowerInvariant()}";
        var full = Resolve(relative) ?? throw new ArgumentException("Invalid evidence path.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, buffer.ToArray());
        return relative;
    }

    public Task<Stream?> OpenReadAsync(string path)
    {
        var full = Resolve(path);
        return Task.FromResult<Stream?>(full is not null && File.Exists(full)
            ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)
            : null);
    }

    public Task DeleteAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var full = Resolve(path);
            if (full is not null && File.Exists(full))
                File.Delete(full);
        }
        return Task.CompletedTask;
    }

    public Task DeleteForPersonAsync(Guid personId)
    {
        var folder = Resolve(personId.ToString() + "/");
        if (folder is not null && Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Full path if it stays inside the root, otherwise null.</summary>
    private string? Resolve(string relative)
    {
        var root = Path.GetFullPath(rootPath);
        // Older rows were stored with Windows separators; accept both.
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', '/')));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
