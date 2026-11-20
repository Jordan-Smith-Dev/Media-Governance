using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services.Audit;

namespace Umbraco.Community.MediaGovernance.Handlers;

/// <summary>Removes governance data (tags and settings) when a media item is permanently deleted.</summary>
internal class MediaDeletedHandler
	: INotificationAsyncHandler<MediaDeletedNotification>
{
	private readonly IImageRepository _images;
	private readonly IAuditService _audit;

	public MediaDeletedHandler(IImageRepository images, IAuditService audit)
	{
		_images = images;
		_audit = audit;
	}

	public async Task HandleAsync(MediaDeletedNotification notification, CancellationToken cancellationToken)
	{
		foreach (var media in notification.DeletedEntities)
		{
			await _images.DeleteForMediaAsync(media.Key);
			await _audit.WriteAsync(Actors.System, "image.deleted", media.Key);
		}
	}
}
