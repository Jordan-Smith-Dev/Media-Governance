namespace Umbraco.Community.MediaGovernance.Services;

public interface IEvidenceStore
{
    /// <summary>Stores a file and returns its path relative to the store root. The client filename is used only for its extension.</summary>
    Task<string> SaveAsync(Guid personId, Guid consentId, string fileName, Stream data);

    /// <summary>Deletes stored files by relative path. Paths that resolve outside the store root are ignored.</summary>
    Task DeleteAsync(IEnumerable<string> paths);

    /// <summary>Deletes the whole folder holding a person's evidence (also catches orphaned files).</summary>
    Task DeleteForPersonAsync(Guid personId);
}

public class EvidenceStore(string rootPath) : IEvidenceStore
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".png", ".jpg", ".jpeg" };

    public async Task<string> SaveAsync(Guid personId, Guid consentId, string fileName, Stream data)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty));
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            throw new ArgumentException("Evidence must be a PDF, PNG or JPG file.", nameof(fileName));

        // Read at most MaxBytes + 1 so oversized uploads are rejected without buffering them in full.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await data.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
                throw new ArgumentException("Evidence files must be 10 MB or smaller.", nameof(data));
        }

        var relative = Path.Combine(personId.ToString(), consentId + extension.ToLowerInvariant());
        var full = Resolve(relative) ?? throw new ArgumentException("Invalid evidence path.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, buffer.ToArray());
        return relative;
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
        var folder = Resolve(personId.ToString() + Path.DirectorySeparatorChar);
        if (folder is not null && Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Full path if it stays inside the root, otherwise null.</summary>
    private string? Resolve(string relative)
    {
        var root = Path.GetFullPath(rootPath);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
