using System.Text.RegularExpressions;
using Umbraco.Cms.Core.IO;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Where evidence bytes physically live. Paths are always relative, forward-slashed, and already validated by <see cref="EvidenceStore"/>.</summary>
public interface IEvidenceFiles
{
    Task WriteAsync(string path, byte[] data);
    Task<Stream?> OpenReadAsync(string path);
    Task<long?> SizeAsync(string path);
    Task DeleteAsync(string path);

    /// <summary>Deletes a folder and everything in it. Does nothing if it is not there.</summary>
    Task DeleteFolderAsync(string folder);
}

/// <summary>Stores consent evidence (a signed form or email) for people.</summary>
public interface IEvidenceStore
{
    /// <summary>Checks and stores a file for a person and returns its relative path. The client filename is used only for its extension.</summary>
    Task<string> SaveAsync(Guid personId, string fileName, Stream data);

    /// <summary>Opens a stored file for reading, or returns null if it is missing or the path is not one this store made.</summary>
    Task<Stream?> OpenReadAsync(string path);

    /// <summary>Size in bytes of a stored file, or null if it is missing or the path is not one this store made.</summary>
    Task<long?> GetSizeAsync(string path);

    /// <summary>Deletes a stored file. Does nothing for a path this store did not make.</summary>
    Task DeleteAsync(string path);

    /// <summary>Deletes the whole folder holding a person's evidence (also catches orphaned files).</summary>
    Task DeleteForPersonAsync(Guid personId);
}

/// <summary>Which evidence file types are accepted, what their bytes must look like, and the content type to serve them with.</summary>
public static class EvidenceContent
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF];

    // person folder / file, both GUIDs, with an allowed extension. Anything else is not ours and is never touched.
    private static readonly Regex StoredPath = new(@"^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}/[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\.(pdf|png|jpg|jpeg)$", RegexOptions.Compiled);

    public static bool IsStoredPath(string? path) => path is not null && StoredPath.IsMatch(path);

    public static bool IsAllowedExtension(string extension) => ContentTypeFor(extension) is not null;

    /// <summary>The content type for an allowed extension, otherwise null. Never derived from anything the client sent.</summary>
    public static string? ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => null,
    };

    /// <summary>A short label for the file type, for display.</summary>
    public static string TypeLabel(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "PDF",
        ".png" => "PNG",
        ".jpg" or ".jpeg" => "JPG",
        _ => "File",
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

    /// <summary>Reads an upload, refusing anything that is not a real PDF, PNG or JPG of 10 MB or less.</summary>
    public static async Task<(string Extension, byte[] Bytes)> ReadAndCheckAsync(string fileName, Stream data)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !IsAllowedExtension(extension))
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

        if (!MatchesExtension(extension, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            throw new ArgumentException("The file contents do not match its type. Evidence must be a real PDF, PNG or JPG.");

        return (extension, buffer.ToArray());
    }
}

public class EvidenceStore(IEvidenceFiles files) : IEvidenceStore
{
    public async Task<string> SaveAsync(Guid personId, string fileName, Stream data)
    {
        var (extension, bytes) = await EvidenceContent.ReadAndCheckAsync(fileName, data);
        // The name is ours (never the client's), and always forward slashes so it works on any operating system.
        var relative = $"{personId}/{Guid.NewGuid()}{extension}";
        await files.WriteAsync(relative, bytes);
        return relative;
    }

    public Task<Stream?> OpenReadAsync(string path) =>
        EvidenceContent.IsStoredPath(path) ? files.OpenReadAsync(path) : Task.FromResult<Stream?>(null);

    public Task<long?> GetSizeAsync(string path) =>
        EvidenceContent.IsStoredPath(path) ? files.SizeAsync(path) : Task.FromResult<long?>(null);

    public Task DeleteAsync(string path) =>
        EvidenceContent.IsStoredPath(path) ? files.DeleteAsync(path) : Task.CompletedTask;

    public Task DeleteForPersonAsync(Guid personId) => files.DeleteFolderAsync(personId.ToString());
}

/// <summary>Evidence on disk under a folder of its own, outside the website.</summary>
public class DiskEvidenceFiles(string rootPath) : IEvidenceFiles
{
    public async Task WriteAsync(string path, byte[] data)
    {
        var full = Resolve(path) ?? throw new ArgumentException("Invalid evidence path.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, data);
    }

    public Task<Stream?> OpenReadAsync(string path)
    {
        var full = Resolve(path);
        return Task.FromResult<Stream?>(full is not null && File.Exists(full)
            ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)
            : null);
    }

    public Task<long?> SizeAsync(string path)
    {
        var full = Resolve(path);
        return Task.FromResult(full is not null && File.Exists(full) ? new FileInfo(full).Length : (long?)null);
    }

    public Task DeleteAsync(string path)
    {
        var full = Resolve(path);
        if (full is not null && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string folder)
    {
        var full = Resolve(folder + "/");
        if (full is not null && Directory.Exists(full)) Directory.Delete(full, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>Full path if it stays inside the root, otherwise null.</summary>
    private string? Resolve(string relative)
    {
        var root = Path.GetFullPath(rootPath);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', '/')));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}

/// <summary>
/// Evidence inside Umbraco's media file system, in a folder of its own. That folder is never served to the public
/// (the middleware answers 404 for it) and the files are only reached through the authenticated Governance API.
/// </summary>
public class MediaEvidenceFiles(MediaFileManager media, string folder) : IEvidenceFiles
{
    private string In(string relative) => $"{folder.Trim('/')}/{relative}";

    public Task WriteAsync(string path, byte[] data)
    {
        using var stream = new MemoryStream(data);
        media.FileSystem.AddFile(In(path), stream, overrideIfExists: true);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string path) =>
        Task.FromResult<Stream?>(media.FileSystem.FileExists(In(path)) ? media.FileSystem.OpenFile(In(path)) : null);

    public Task<long?> SizeAsync(string path) =>
        Task.FromResult(media.FileSystem.FileExists(In(path)) ? media.FileSystem.GetSize(In(path)) : (long?)null);

    public Task DeleteAsync(string path)
    {
        if (media.FileSystem.FileExists(In(path))) media.FileSystem.DeleteFile(In(path));
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string folderName)
    {
        if (media.FileSystem.DirectoryExists(In(folderName))) media.FileSystem.DeleteDirectory(In(folderName), recursive: true);
        return Task.CompletedTask;
    }
}
