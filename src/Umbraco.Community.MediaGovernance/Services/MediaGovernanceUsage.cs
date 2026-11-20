using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services.Status;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services;

internal class MediaGovernanceUsage : IMediaGovernanceUsage
{
	private readonly StatusLookupService _statuses;

	public MediaGovernanceUsage(StatusLookupService statuses)
	{
		_statuses = statuses;
	}

	public async Task<GovernanceStatus> GetStatusAsync(Guid mediaKey) =>
		(await _statuses.GetStatusesAsync([mediaKey])).GetValueOrDefault(mediaKey, GovernanceStatus.NeedsTagging);

	public async Task<bool> IsUsableAsync(Guid mediaKey) => UsagePolicy.IsUsable(await GetStatusAsync(mediaKey));

	public async Task<IReadOnlyDictionary<Guid, bool>> GetUsableAsync(IReadOnlyCollection<Guid> mediaKeys)
	{
		var keys = mediaKeys.Distinct().ToList();
		var map = await _statuses.GetStatusesAsync(keys);
		return keys.ToDictionary(k => k, k => UsagePolicy.IsUsable(map.GetValueOrDefault(k, GovernanceStatus.NeedsTagging)));
	}
}
