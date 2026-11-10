using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Works out which media folders contain images that cannot be used, or that need attention.</summary>
public class FolderIssuesService(IEntityService entities, StatusLookupService statuses, MediaGateCache cache)
{
    private const string StatusesCacheKey = "mg-all-image-statuses";

    public async Task<IReadOnlyDictionary<Guid, FolderAttention>> GetAsync(IReadOnlyCollection<Guid> folderKeys)
    {
        var all = entities.GetAll(UmbracoObjectTypes.Media).ToList();
        var images = all.OfType<IContentEntitySlim>()
            .Where(e => !e.Trashed && string.Equals(e.ContentTypeAlias, ImageCatalog.ImageTypeAlias, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var statusByImage = await StatusesAsync(images.Select(i => i.Key).ToList());

        var result = new Dictionary<Guid, FolderAttention>();
        foreach (var key in folderKeys)
        {
            var folder = all.FirstOrDefault(e => e.Key == key);
            if (folder is null) continue;

            // An entity's path is its ancestors' ids, e.g. "-1,1056,1060": inside a folder when the folder's id is on it.
            var marker = $",{folder.Id},";
            var inside = images.Where(i => ("," + i.Path + ",").Contains(marker));
            result[key] = FolderRollup.Of(inside.Select(i => statusByImage.GetValueOrDefault(i.Key, GovernanceStatus.NeedsTagging)));
        }
        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, GovernanceStatus>> StatusesAsync(IReadOnlyCollection<Guid> imageKeys)
    {
        if (cache.TryGet<IReadOnlyDictionary<Guid, GovernanceStatus>>(StatusesCacheKey, out var cached) && cached is not null)
            return cached;

        var fresh = await statuses.GetStatusesAsync(imageKeys);
        cache.Set(StatusesCacheKey, fresh);
        return fresh;
    }
}
