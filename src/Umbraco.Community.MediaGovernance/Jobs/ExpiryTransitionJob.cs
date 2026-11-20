using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Notifications;
using Umbraco.Community.MediaGovernance.Services.Overview;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Jobs;

/// <summary>
/// Daily check for images whose status has changed into one that needs action, with one digest email.
/// Runs only on the scheduling server, so load-balanced sites do not send duplicates.
/// </summary>
internal class ExpiryTransitionJob : IRecurringBackgroundJob
{
	private readonly IServiceScopeFactory _scopes;
	private readonly ILogger<ExpiryTransitionJob> _logger;

	public ExpiryTransitionJob(IServiceScopeFactory scopes, ILogger<ExpiryTransitionJob> logger)
	{
		_scopes = scopes;
		_logger = logger;
	}

	public TimeSpan Period => TimeSpan.FromHours(24);
	public TimeSpan Delay => TimeSpan.FromMinutes(2);
	public ServerRole[] ServerRoles => IRecurringBackgroundJob.DefaultServerRoles;

	public event EventHandler PeriodChanged { add { } remove { } }

	/// <summary>File name with extension per image, e.g. "team-2021.jpg", taken from the recorded file path.</summary>
	private static async Task<IReadOnlyDictionary<Guid, string>> FileNamesAsync(IServiceProvider sp) =>
		(await sp.GetRequiredService<IMediaFileRepository>().ListPathsAsync())
			.ToDictionary(p => p.Key, p => Path.GetFileName(p.Value));

	public async Task RunJobAsync()
	{
		using var scope = _scopes.CreateScope();
		var sp = scope.ServiceProvider;
		var overview = sp.GetRequiredService<OverviewService>();
		var notified = sp.GetRequiredService<INotifiedRepository>();
		var catalog = sp.GetRequiredService<IImageCatalog>();

		var current = await overview.StatusMapAsync();
		var last = await notified.GetAllAsync();
		var items = current.Select(c => (c.Key, c.Value, last.TryGetValue(c.Key, out var l) ? (Core.Models.GovernanceStatus?)l : null)).ToList();

		var transitions = TransitionDetector.Detect(items);
		var sent = true;
		if (transitions.Count > 0)
		{
			var names = (await catalog.ListImagesAsync()).ToDictionary(i => i.Key, i => i.Name);
			sent = await sp.GetRequiredService<NotificationService>().SendAsync(transitions, names, await FileNamesAsync(sp));
			if (sent)
				_logger.LogInformation("Media governance: {Count} image(s) need attention.", transitions.Count);
			else
				_logger.LogInformation("Media governance: {Count} image(s) need attention, but no digest was sent (no recipients or email unavailable); will retry.", transitions.Count);
		}

		// If nothing was sent, leave attention statuses unrecorded so they are reported once email works.
		foreach (var (key, status) in TransitionDetector.StatusesToRecord(items, sent))
			await notified.UpsertAsync(key, status);
	}
}
