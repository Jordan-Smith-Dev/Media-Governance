using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;

namespace Umbraco.Community.MediaGovernance.Services;

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

public class MediaFileIndex(
    IMediaFileRepository files,
    IImageCatalog catalog,
    IMediaService media,
    MediaGateCache cache) : IMediaFileIndex
{
    public async Task<Guid?> FindMediaKeyAsync(string normalizedPath)
    {
        var cacheKey = "mg-path-" + normalizedPath;
        if (cache.TryGet<Guid>(cacheKey, out var cached))
            return cached == Guid.Empty ? null : cached;

        var key = await files.FindKeyAsync(normalizedPath);
        cache.Set(cacheKey, key ?? Guid.Empty);   // misses are cached too, so unrelated files cost nothing
        return key;
    }

    public async Task RefreshAsync(Guid mediaKey)
    {
        var item = media.GetById(mediaKey);
        var path = MediaFilePath.Normalize(MediaFilePath.ExtractSrc(item?.GetValue<string>("umbracoFile")));

        if (path is null)
            await files.DeleteAsync(mediaKey);
        else
            await files.UpsertAsync(mediaKey, path);

        cache.Clear();
    }

    public async Task RefreshAllAsync()
    {
        // Every image is indexed, not only governed ones: an old image with no dates is judged by its upload date,
        // so it can be withheld too and its file must be covered. Already-indexed images are left alone here;
        // the saved handler keeps them current when their file changes.
        var indexed = (await files.ListKeysAsync()).ToHashSet();
        foreach (var image in await catalog.ListImagesAsync())
        {
            if (!indexed.Contains(image.Key))
                await RefreshAsync(image.Key);
        }
    }

    public void ClearCache() => cache.Clear();
}
