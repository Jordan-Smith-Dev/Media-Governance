using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Handlers;

/// <summary>Removes governance data (tags, consent records, evidence files) when a media item is permanently deleted.</summary>
public class MediaDeletedHandler(
    IImageRepository images,
    IEvidenceStore evidence,
    IAuditService audit,
    ILogger<MediaDeletedHandler> logger)
    : INotificationAsyncHandler<MediaDeletedNotification>
{
    public async Task HandleAsync(MediaDeletedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var media in notification.DeletedEntities)
        {
            var paths = await images.DeleteForMediaAsync(media.Key);
            try
            {
                await evidence.DeleteAsync(paths);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Evidence files for deleted media {MediaKey} could not be fully deleted.", media.Key);
            }
            await audit.WriteAsync(Actors.System, "image.deleted", media.Key);
        }
    }
}
