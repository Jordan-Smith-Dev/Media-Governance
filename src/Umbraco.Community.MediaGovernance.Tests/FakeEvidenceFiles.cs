using Umbraco.Community.MediaGovernance.Services.Evidence;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeEvidenceFiles : IEvidenceFiles
{
	public Dictionary<string, byte[]> Files { get; } = new();
	public List<string> FoldersDeleted { get; } = new();
	public bool FailOnDeleteFolder { get; set; }

	public Task WriteAsync(string path, byte[] data) { Files[path] = data; return Task.CompletedTask; }
	public Task<Stream?> OpenReadAsync(string path) => Task.FromResult<Stream?>(Files.TryGetValue(path, out var b) ? new MemoryStream(b) : null);
	public Task<long?> SizeAsync(string path) => Task.FromResult(Files.TryGetValue(path, out var b) ? (long?)b.Length : null);
	public Task DeleteAsync(string path) { Files.Remove(path); return Task.CompletedTask; }
	public Task DeleteFolderAsync(string folder)
	{
		if (FailOnDeleteFolder)
			throw new IOException("locked");
		FoldersDeleted.Add(folder);
		foreach (var k in Files.Keys.Where(k => k.StartsWith(folder + "/")).ToList())
			Files.Remove(k);
		return Task.CompletedTask;
	}
}
