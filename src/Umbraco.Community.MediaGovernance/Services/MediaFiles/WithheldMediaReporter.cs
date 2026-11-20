using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

/// <summary>
/// Writes a warning to the Umbraco log the first time a page tries to show withheld media, then at most once per
/// window for that page, so a busy page does not flood the log. Only the media key, status and page path are
/// logged: no names, no consent details and no query strings.
/// </summary>
internal class WithheldMediaReporter : IWithheldMediaReporter
{
	private static readonly TimeSpan Window = TimeSpan.FromHours(6);
	private const int PruneAbove = 10_000;

	private readonly ILogger<WithheldMediaReporter> _logger;
	private readonly TimeProvider _time;

	public WithheldMediaReporter(ILogger<WithheldMediaReporter> logger, TimeProvider time)
	{
		_logger = logger;
		_time = time;
	}

	private readonly ConcurrentDictionary<(Guid Media, GovernanceStatus Status, string Path), DateTimeOffset> _lastLogged = new();

	public void Report(Guid mediaKey, GovernanceStatus status, string? pagePath)
	{
		var now = _time.GetUtcNow();
		var key = (mediaKey, status, pagePath ?? string.Empty);

		if (_lastLogged.TryGetValue(key, out var last) && now - last < Window)
			return;
		_lastLogged[key] = now;

		if (_lastLogged.Count > PruneAbove)
		{
			foreach (var stale in _lastLogged.Where(e => now - e.Value >= Window).Select(e => e.Key).ToList())
				_lastLogged.TryRemove(stale, out _);
		}

		_logger.LogWarning(
			"Media governance withheld {Status} media {MediaKey} on page {PagePath}. Remove or replace it on this page.",
			status, mediaKey, pagePath ?? "(unknown page)");
	}
}
