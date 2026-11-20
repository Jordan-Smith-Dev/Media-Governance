using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Core;

/// <summary>Decides whether an image may be shown on the public site, given its governance status.</summary>
internal static class UsagePolicy
{
	/// <summary>
	/// Expired images must stop being used, and images of people who have not consented must not be
	/// shown. Everything else is usable: "Expiring soon" is only a warning, and "Needs tagging" means
	/// nobody has recorded who is pictured yet, so there is no known problem to act on.
	/// </summary>
	public static bool IsUsable(GovernanceStatus status) =>
		status is not (GovernanceStatus.Expired or GovernanceStatus.NoConsent);
}
