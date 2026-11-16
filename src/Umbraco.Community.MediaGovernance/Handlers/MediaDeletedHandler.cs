using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Handlers;

/// <summary>Removes governance data (tags and settings) when a media item is permanently deleted.</summary>
public class MediaDeletedHandler(
    IImageRepository images,
    IAuditService audit)
    : INotificationAsyncHandler<MediaDeletedNotification>
{
    public async Task HandleAsync(MediaDeletedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var media in notification.DeletedEntities)
        {
            await images.DeleteForMediaAsync(media.Key);
            await audit.WriteAsync(Actors.System, "image.deleted", media.Key);
        }
    }
}
