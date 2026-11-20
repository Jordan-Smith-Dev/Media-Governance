using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.Overview;

public record OverviewQuery(
	GovernanceStatus? Status,
	string? Filter,
	int Skip,
	int Take,
	IReadOnlyCollection<Guid>? Keys = null,
	OverviewSort Sort = OverviewSort.Name,
	bool Descending = false);
