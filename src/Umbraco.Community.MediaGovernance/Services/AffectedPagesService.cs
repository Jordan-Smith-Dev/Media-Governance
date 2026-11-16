using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

public record MediaReferrer(Guid PageKey, string PageName, bool Published, string? ContentTypeName, string? ContentTypeIcon);

/// <summary>Finds the pages (documents) that use a media item.</summary>
public interface IMediaReferenceSource
{
    Task<IReadOnlyList<MediaReferrer>> GetPagesUsingAsync(Guid mediaKey);
}

/// <summary>Uses Umbraco's own tracked references, the data behind "Referenced by" on a media item.</summary>
public class TrackedMediaReferenceSource(ITrackedReferencesService tracked) : IMediaReferenceSource
{
    private const int PageSize = 100;
    private const int MaxPerImage = 1000;

    public async Task<IReadOnlyList<MediaReferrer>> GetPagesUsingAsync(Guid mediaKey)
    {
        var found = new Dictionary<Guid, MediaReferrer>();
        for (long skip = 0; skip < MaxPerImage; skip += PageSize)
        {
            var page = await tracked.GetPagedRelationsForItemAsync(mediaKey, skip, PageSize, false);
            foreach (var item in page.Items)
            {
                if (!string.Equals(item.NodeType, "document", StringComparison.OrdinalIgnoreCase)) continue;
                found[item.NodeKey] = new MediaReferrer(item.NodeKey, item.NodeName ?? string.Empty, item.NodePublished ?? false, item.ContentTypeName, item.ContentTypeIcon);
            }
            if (skip + PageSize >= page.Total) break;
        }
        return found.Values.ToList();
    }
}

public record AffectedImage(Guid MediaKey, string Name, GovernanceStatus Status);

public record AffectedPage(Guid PageKey, string Name, bool Published, string? ContentType, string? ContentTypeIcon, IReadOnlyList<AffectedImage> Images);

/// <summary>Lists the pages that use expired images or images without consent, so editors know what to fix.</summary>
public class AffectedPagesService(IImageCatalog catalog, StatusLookupService statuses, IMediaReferenceSource references)
{
    /// <summary>Upper bound on withheld images examined in one request, to keep the dashboard responsive on very large libraries.</summary>
    public const int MaxWithheldImages = 500;

    public async Task<IReadOnlyList<AffectedPage>> GetAsync()
    {
        var images = await catalog.ListImagesAsync();
        var status = await statuses.GetStatusesAsync(images.Select(i => i.Key).ToList());

        var withheld = images
            .Where(i => status.TryGetValue(i.Key, out var s) && !UsagePolicy.IsUsable(s))
            .Take(MaxWithheldImages)
            .ToList();

        var pages = new Dictionary<Guid, (MediaReferrer Page, List<AffectedImage> Images)>();
        foreach (var image in withheld)
        {
            foreach (var page in await references.GetPagesUsingAsync(image.Key))
            {
                if (!pages.TryGetValue(page.PageKey, out var entry))
                    pages[page.PageKey] = entry = (page, new List<AffectedImage>());
                entry.Images.Add(new AffectedImage(image.Key, image.Name, status[image.Key]));
            }
        }

        return pages.Values
            .Select(e => new AffectedPage(
                e.Page.PageKey, e.Page.PageName, e.Page.Published, e.Page.ContentTypeName, e.Page.ContentTypeIcon,
                e.Images.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
