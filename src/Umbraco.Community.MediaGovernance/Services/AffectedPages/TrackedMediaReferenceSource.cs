using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Services.AffectedPages;

/// <summary>Uses Umbraco's own tracked references, the data behind "Referenced by" on a media item.</summary>
internal class TrackedMediaReferenceSource : IMediaReferenceSource
{
	private const int PageSize = 100;
	private const int MaxPerImage = 1000;

	private readonly ITrackedReferencesService _tracked;

	public TrackedMediaReferenceSource(ITrackedReferencesService tracked)
	{
		_tracked = tracked;
	}

	public async Task<IReadOnlyList<MediaReferrer>> GetPagesUsingAsync(Guid mediaKey)
	{
		var found = new Dictionary<Guid, MediaReferrer>();
		for (long skip = 0; skip < MaxPerImage; skip += PageSize)
		{
			var page = await _tracked.GetPagedRelationsForItemAsync(mediaKey, skip, PageSize, false);
			foreach (var item in page.Items)
			{
				if (!string.Equals(item.NodeType, "document", StringComparison.OrdinalIgnoreCase))
					continue;
				found[item.NodeKey] = new MediaReferrer(item.NodeKey, item.NodeName ?? string.Empty, item.NodePublished ?? false, item.ContentTypeName, item.ContentTypeIcon);
			}
			if (skip + PageSize >= page.Total)
				break;
		}
		return found.Values.ToList();
	}
}
