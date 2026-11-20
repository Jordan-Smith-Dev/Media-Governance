namespace Umbraco.Community.MediaGovernance.Services.Evidence;

/// <summary>Evidence on disk under a folder of its own, outside the website.</summary>
internal class DiskEvidenceFiles : IEvidenceFiles
{
	private readonly string _rootPath;

	public DiskEvidenceFiles(string rootPath)
	{
		_rootPath = rootPath;
	}

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
		if (full is not null && File.Exists(full))
			File.Delete(full);
		return Task.CompletedTask;
	}

	public Task DeleteFolderAsync(string folder)
	{
		var full = Resolve(folder + "/");
		if (full is not null && Directory.Exists(full))
			Directory.Delete(full, recursive: true);
		return Task.CompletedTask;
	}

	/// <summary>Full path if it stays inside the root, otherwise null.</summary>
	private string? Resolve(string relative)
	{
		var root = Path.GetFullPath(_rootPath);
		var full = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', '/')));
		return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
	}
}
