namespace Umbraco.Community.MediaGovernance.Services.Evidence;

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
