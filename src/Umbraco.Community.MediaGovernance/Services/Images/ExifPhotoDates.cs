using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;

namespace Umbraco.Community.MediaGovernance.Services.Images;

internal class ExifPhotoDates : IPhotoDates
{
	private readonly IMediaService _media;
	private readonly MediaFileManager _files;

	public ExifPhotoDates(IMediaService media, MediaFileManager files)
	{
		_media = media;
		_files = files;
	}

	public Task<DateOnly?> FindAsync(Guid mediaKey)
	{
		var item = _media.GetById(mediaKey);
		var src = MediaFilePath.ExtractSrc(item?.GetValue<string>("umbracoFile"));
		if (string.IsNullOrWhiteSpace(src))
			return Task.FromResult<DateOnly?>(null);
		if (!src.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !src.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
			return Task.FromResult<DateOnly?>(null);

		try
		{
			var path = _files.FileSystem.GetRelativePath(src);
			if (!_files.FileSystem.FileExists(path))
				return Task.FromResult<DateOnly?>(null);
			using var stream = _files.FileSystem.OpenFile(path);
			return Task.FromResult(ExifDate.Read(stream));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return Task.FromResult<DateOnly?>(null);
		}
	}
}
