using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.Overview;

public record OverviewItem(
	Guid MediaKey,
	string Name,
	string Folder,
	IReadOnlyList<string> People,
	DateOnly? DateTaken,
	DateOnly? Expiry,
	int? DaysToExpiry,
	string Consent,
	GovernanceStatus Status,
	ExpirySource ExpirySource = ExpirySource.None,
	IReadOnlyList<GovernanceStatus>? Issues = null,
	bool NoPeople = false);
