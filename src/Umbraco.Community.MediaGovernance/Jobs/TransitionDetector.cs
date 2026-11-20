using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Jobs;

internal static class TransitionDetector
{
	private static readonly HashSet<GovernanceStatus> NeedsAttention =
		[GovernanceStatus.ExpiringSoon, GovernanceStatus.Expired, GovernanceStatus.NoConsent, GovernanceStatus.NeedsTagging];

	/// <summary>
	/// Images that have just changed into a status someone should act on. An image whose status
	/// is unchanged since the last run is not reported again.
	/// </summary>
	public static IReadOnlyList<Transition> Detect(IEnumerable<(Guid Key, GovernanceStatus Current, GovernanceStatus? Last)> items) =>
		items.Where(i => i.Current != i.Last && NeedsAttention.Contains(i.Current))
			.Select(i => new Transition(i.Key, i.Current))
			.ToList();

	/// <summary>
	/// Every changed status is recorded (not only attention ones), so an image that returns to
	/// Compliant and later expires again is reported again.
	/// </summary>
	public static IReadOnlyDictionary<Guid, GovernanceStatus> StatusesToRecord(
		IEnumerable<(Guid Key, GovernanceStatus Current, GovernanceStatus? Last)> items, bool notificationSent = true) =>
		items.Where(i => i.Current != i.Last && (notificationSent || !NeedsAttention.Contains(i.Current)))
			.ToDictionary(i => i.Key, i => i.Current);
}
