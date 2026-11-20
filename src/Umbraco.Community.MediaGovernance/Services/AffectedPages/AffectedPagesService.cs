using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Status;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.AffectedPages;

/// <summary>Lists the pages that use expired images or images without consent, so editors know what to fix.</summary>
public class AffectedPagesService
{
	private readonly IImageCatalog _catalog;
	private readonly StatusLookupService _statuses;
	private readonly IMediaReferenceSource _references;

	public AffectedPagesService(IImageCatalog catalog, StatusLookupService statuses, IMediaReferenceSource references)
	{
		_catalog = catalog;
		_statuses = statuses;
		_references = references;
	}

	/// <summary>Upper bound on withheld images examined in one request, to keep the dashboard responsive on very large libraries.</summary>
	public const int MaxWithheldImages = 500;

	public async Task<IReadOnlyList<AffectedPage>> GetAsync()
	{
		var images = await _catalog.ListImagesAsync();
		var status = await _statuses.GetStatusesAsync(images.Select(i => i.Key).ToList());

		var withheld = images
			.Where(i => status.TryGetValue(i.Key, out var s) && !UsagePolicy.IsUsable(s))
			.Take(MaxWithheldImages)
			.ToList();

		var pages = new Dictionary<Guid, (MediaReferrer Page, List<AffectedImage> Images)>();
		foreach (var image in withheld)
		{
			foreach (var page in await _references.GetPagesUsingAsync(image.Key))
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
