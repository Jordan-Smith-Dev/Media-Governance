using Umbraco.Cms.Core.IO;

namespace Umbraco.Community.MediaGovernance.Services.Evidence;

/// <summary>
/// Evidence inside Umbraco's media file system, in a folder of its own. That folder is never served to the public
/// (the middleware answers 404 for it) and the files are only reached through the authenticated Governance API.
/// </summary>
internal class MediaEvidenceFiles : IEvidenceFiles
{
	private readonly MediaFileManager _media;
	private readonly string _folder;

	public MediaEvidenceFiles(MediaFileManager media, string folder)
	{
		_media = media;
		_folder = folder;
	}

	private string In(string relative) => $"{_folder.Trim('/')}/{relative}";

	public Task WriteAsync(string path, byte[] data)
	{
		using var stream = new MemoryStream(data);
		_media.FileSystem.AddFile(In(path), stream, overrideIfExists: true);
		return Task.CompletedTask;
	}

	public Task<Stream?> OpenReadAsync(string path) =>
		Task.FromResult<Stream?>(_media.FileSystem.FileExists(In(path)) ? _media.FileSystem.OpenFile(In(path)) : null);

	public Task<long?> SizeAsync(string path) =>
		Task.FromResult(_media.FileSystem.FileExists(In(path)) ? _media.FileSystem.GetSize(In(path)) : (long?)null);

	public Task DeleteAsync(string path)
	{
		if (_media.FileSystem.FileExists(In(path)))
			_media.FileSystem.DeleteFile(In(path));
		return Task.CompletedTask;
	}

	public Task DeleteFolderAsync(string folderName)
	{
		if (_media.FileSystem.DirectoryExists(In(folderName)))
			_media.FileSystem.DeleteDirectory(In(folderName), recursive: true);
		return Task.CompletedTask;
	}
}
