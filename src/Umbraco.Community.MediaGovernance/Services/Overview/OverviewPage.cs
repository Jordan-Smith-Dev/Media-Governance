using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.Overview;

public record OverviewPage(
	IReadOnlyDictionary<GovernanceStatus, int> Counts,
	int AllCount,
	IReadOnlyList<OverviewItem> Items,
	int Total);
