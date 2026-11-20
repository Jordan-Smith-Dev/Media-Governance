using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services.Images;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

internal class MediaFileIndex : IMediaFileIndex
{
	private readonly IMediaFileRepository _files;
	private readonly IImageCatalog _catalog;
	private readonly IMediaService _media;
	private readonly MediaGateCache _cache;

	public MediaFileIndex(IMediaFileRepository files, IImageCatalog catalog, IMediaService media, MediaGateCache cache)
	{
		_files = files;
		_catalog = catalog;
		_media = media;
		_cache = cache;
	}

	public async Task<Guid?> FindMediaKeyAsync(string normalizedPath)
	{
		var cacheKey = "mg-path-" + normalizedPath;
		if (_cache.TryGet<Guid>(cacheKey, out var cached))
			return cached == Guid.Empty ? null : cached;

		var key = await _files.FindKeyAsync(normalizedPath);
		_cache.Set(cacheKey, key ?? Guid.Empty);   // misses are cached too, so unrelated files cost nothing
		return key;
	}

	public async Task RefreshAsync(Guid mediaKey)
	{
		var item = _media.GetById(mediaKey);
		var path = MediaFilePath.Normalize(MediaFilePath.ExtractSrc(item?.GetValue<string>("umbracoFile")));

		if (path is null)
			await _files.DeleteAsync(mediaKey);
		else
			await _files.UpsertAsync(mediaKey, path);

		_cache.Clear();
	}

	public async Task RefreshAllAsync()
	{
		// Every image is indexed, not only governed ones: an old image with no dates is judged by its upload date,
		// so it can be withheld too and its file must be covered. Already-indexed images are left alone here;
		// the saved handler keeps them current when their file changes.
		var indexed = (await _files.ListKeysAsync()).ToHashSet();
		foreach (var image in await _catalog.ListImagesAsync())
		{
			if (!indexed.Contains(image.Key))
				await RefreshAsync(image.Key);
		}
	}

	public void ClearCache() => _cache.Clear();
}
