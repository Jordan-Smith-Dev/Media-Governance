using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;

namespace Umbraco.Community.MediaGovernance.Handlers;

/// <summary>At startup, records the file path of every governed image (covers upgrades and files changed while offline).</summary>
internal class MediaFileIndexStartupHandler
	: INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
	private readonly IServiceScopeFactory _scopes;

	public MediaFileIndexStartupHandler(IServiceScopeFactory scopes)
	{
		_scopes = scopes;
	}

	public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
	{
		using var scope = _scopes.CreateScope();
		await scope.ServiceProvider.GetRequiredService<IMediaFileIndex>().RefreshAllAsync();
	}
}
