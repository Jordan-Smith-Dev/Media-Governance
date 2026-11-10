using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Image media items from the Umbraco media library (media type alias "Image").</summary>
public class ImageCatalog(IEntityService entities) : IImageCatalog
{
    public const string ImageTypeAlias = "Image";

    public Task<IReadOnlyList<ImageInfo>> ListImagesAsync()
    {
        var all = entities.GetAll(UmbracoObjectTypes.Media).ToList();
        var namesById = all.ToDictionary(e => e.Id, e => e.Name ?? string.Empty);

        IReadOnlyList<ImageInfo> images = all
            .OfType<IContentEntitySlim>()
            .Where(e => !e.Trashed)
            .Where(e => string.Equals(e.ContentTypeAlias, ImageTypeAlias, StringComparison.OrdinalIgnoreCase))
            .Select(e => new ImageInfo(e.Key, e.Name ?? string.Empty, namesById.GetValueOrDefault(e.ParentId, string.Empty)))
            .ToList();
        return Task.FromResult(images);
    }

    /// <summary>True when the key belongs to an image media item.</summary>
    public bool IsImage(Guid mediaKey) =>
        entities.Get(mediaKey, UmbracoObjectTypes.Media) is IContentEntitySlim { Trashed: false } e
        && string.Equals(e.ContentTypeAlias, ImageTypeAlias, StringComparison.OrdinalIgnoreCase);
}
