using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

/// <summary>Decides whether a request for a media file may be served.</summary>
internal class MediaFileGate
{
	private readonly IMediaFileIndex _index;
	private readonly IMediaGovernanceUsage _usage;
	private readonly IWithheldMediaReporter _reporter;
	private readonly MediaGateCache _cache;

	public MediaFileGate(IMediaFileIndex index, IMediaGovernanceUsage usage, IWithheldMediaReporter reporter, MediaGateCache cache)
	{
		_index = index;
		_usage = usage;
		_reporter = reporter;
		_cache = cache;
	}

	public static readonly GateDecision Allow = new(true);

	public async Task<GateDecision> CheckAsync(string requestPath, bool isBackofficeUser)
	{
		// Editors must always be able to see an image, or they could not find and fix it.
		if (isBackofficeUser)
			return Allow;

		var normalized = MediaFilePath.Normalize(requestPath);
		if (normalized is null)
			return Allow;

		var key = await _index.FindMediaKeyAsync(normalized);
		if (key is null)
			return Allow;

		var statusKey = "mg-status-" + key;
		if (!_cache.TryGet<GovernanceStatus>(statusKey, out var status))
		{
			status = await _usage.GetStatusAsync(key.Value);
			_cache.Set(statusKey, status);
		}

		if (UsagePolicy.IsUsable(status))
			return Allow;

		_reporter.Report(key.Value, status, $"{requestPath} (direct file request)");
		return new GateDecision(false, status);
	}

	public void ClearCache() => _cache.Clear();
}
