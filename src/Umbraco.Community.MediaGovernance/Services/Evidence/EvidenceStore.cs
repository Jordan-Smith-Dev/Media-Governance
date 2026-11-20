namespace Umbraco.Community.MediaGovernance.Services.Evidence;

public class EvidenceStore : IEvidenceStore
{
	private readonly IEvidenceFiles _files;

	public EvidenceStore(IEvidenceFiles files)
	{
		_files = files;
	}

	public async Task<string> SaveAsync(Guid personId, string fileName, Stream data)
	{
		var (extension, bytes) = await EvidenceContent.ReadAndCheckAsync(fileName, data);
		// The name is ours (never the client's), and always forward slashes so it works on any operating system.
		var relative = $"{personId}/{Guid.NewGuid()}{extension}";
		await _files.WriteAsync(relative, bytes);
		return relative;
	}

	public Task<Stream?> OpenReadAsync(string path) =>
		EvidenceContent.IsStoredPath(path) ? _files.OpenReadAsync(path) : Task.FromResult<Stream?>(null);

	public Task<long?> GetSizeAsync(string path) =>
		EvidenceContent.IsStoredPath(path) ? _files.SizeAsync(path) : Task.FromResult<long?>(null);

	public Task DeleteAsync(string path) =>
		EvidenceContent.IsStoredPath(path) ? _files.DeleteAsync(path) : Task.CompletedTask;

	public Task DeleteForPersonAsync(Guid personId) => _files.DeleteFolderAsync(personId.ToString());
}
