using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;

namespace Umbraco.Community.MediaGovernance.Handlers;

/// <summary>When an image is saved (new, or its file replaced), re-records its file path.</summary>
internal class MediaFileIndexSavedHandler
	: INotificationAsyncHandler<MediaSavedNotification>
{
	private readonly IMediaFileIndex _index;

	public MediaFileIndexSavedHandler(IMediaFileIndex index)
	{
		_index = index;
	}

	public async Task HandleAsync(MediaSavedNotification notification, CancellationToken cancellationToken)
	{
		foreach (var media in notification.SavedEntities)
		{
			if (string.Equals(media.ContentType.Alias, ImageCatalog.ImageTypeAlias, StringComparison.OrdinalIgnoreCase))
				await _index.RefreshAsync(media.Key);
		}
	}
}
