using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Handlers;

/// <summary>At startup, records the file path of every governed image (covers upgrades and files changed while offline).</summary>
public class MediaFileIndexStartupHandler(IServiceScopeFactory scopes)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediaFileIndex>().RefreshAllAsync();
    }
}

/// <summary>When an image is saved (new, or its file replaced), re-records its file path.</summary>
public class MediaFileIndexSavedHandler(IMediaFileIndex index)
    : INotificationAsyncHandler<MediaSavedNotification>
{
    public async Task HandleAsync(MediaSavedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var media in notification.SavedEntities)
        {
            if (string.Equals(media.ContentType.Alias, ImageCatalog.ImageTypeAlias, StringComparison.OrdinalIgnoreCase))
                await index.RefreshAsync(media.Key);
        }
    }
}
