namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

/// <summary>Maps a media file's URL path to the media item it belongs to (for governed images only).</summary>
public interface IMediaFileIndex
{
	/// <summary>The governed media item that owns this normalised path, or null.</summary>
	Task<Guid?> FindMediaKeyAsync(string normalizedPath);

	/// <summary>Re-reads the media item's file path (call after its file changes or it is first governed).</summary>
	Task RefreshAsync(Guid mediaKey);

	/// <summary>Records the file path of every image that has none recorded yet.</summary>
	Task RefreshAllAsync();

	void ClearCache();
}
