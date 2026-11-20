using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Core;

internal static class StatusCalculator
{
	/// <summary>
	/// Custom expiry, else date taken (or obtained) plus retention, capped by the earliest
	/// person-level consent expiry. Null when nothing determines an expiry.
	/// </summary>
	public static DateOnly? EffectiveExpiry(ImageGovernanceFacts f, GovernanceSettings s)
	{
		var baseDate = f.DateTaken ?? f.DateObtained ?? f.DateUploaded;
		DateOnly? expiry = f.CustomExpiry ?? baseDate?.AddYears(s.RetentionYears);

		foreach (var personId in f.TaggedPersonIds)
		{
			var own = f.Consents.Where(c => c.PersonId == personId && c.WithdrawnOn is null).ToList();
			if (own.Count == 0 || own.Any(c => c.ExpiresOn is null))
				continue;

			var coverage = own.Max(c => c.ExpiresOn!.Value);
			if (expiry is null || coverage < expiry)
				expiry = coverage;
		}

		return expiry;
	}

	/// <summary>Where <see cref="EffectiveExpiry"/> comes from.</summary>
	public static ExpirySource ExpirySourceOf(ImageGovernanceFacts f, GovernanceSettings s)
	{
		var effective = EffectiveExpiry(f, s);
		if (effective is null)
			return ExpirySource.None;

		var baseDate = f.DateTaken ?? f.DateObtained ?? f.DateUploaded;
		DateOnly? retention = f.CustomExpiry ?? baseDate?.AddYears(s.RetentionYears);
		if (retention is null || effective < retention)
			return ExpirySource.Consent;
		return f.CustomExpiry is not null ? ExpirySource.Custom : ExpirySource.Default;
	}

	/// <summary>
	/// The offered uses that every tagged person has an active consent for. An image with nobody pictured is cleared
	/// for every use; an image nobody has said anything about yet (untagged, not marked "no people") is cleared for none.
	/// </summary>
	public static IReadOnlyList<string> ClearedUses(ImageGovernanceFacts f, IReadOnlyList<string> allUses, DateOnly today)
	{
		if (f.TaggedPersonIds.Count == 0)
			return f.NoPeople ? allUses.ToList() : [];

		IEnumerable<string> cleared = allUses;
		foreach (var personId in f.TaggedPersonIds)
		{
			var consented = f.Consents
				.Where(c => c.PersonId == personId && IsActive(c, today))
				.SelectMany(c => c.PermittedUses ?? [])
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			cleared = cleared.Where(consented.Contains);
		}
		return cleared.ToList();
	}

	/// <summary>
	/// Every problem the image has, most severe first: Expired, No consent, Needs tagging, Expiring soon.
	/// An image can have several at once (expired and also missing consent). Empty means compliant.
	/// </summary>
	public static IReadOnlyList<GovernanceStatus> Issues(ImageGovernanceFacts f, GovernanceSettings s, DateOnly today)
	{
		var issues = new List<GovernanceStatus>();
		var expiry = EffectiveExpiry(f, s);
		var expired = expiry is { } e && e <= today;

		if (expired)
			issues.Add(GovernanceStatus.Expired);
		if (f.TaggedPersonIds.Any(p => !HasActiveConsent(f, p, today)))
			issues.Add(GovernanceStatus.NoConsent);
		if (f.TaggedPersonIds.Count == 0 && !f.NoPeople)
			issues.Add(GovernanceStatus.NeedsTagging);
		if (!expired && expiry is { } soon && soon <= today.AddDays(s.WarningDays))
			issues.Add(GovernanceStatus.ExpiringSoon);

		return issues;
	}

	/// <summary>The headline status: the most severe issue, or Compliant when there are none.</summary>
	public static GovernanceStatus Calculate(ImageGovernanceFacts f, GovernanceSettings s, DateOnly today)
	{
		var issues = Issues(f, s, today);
		return issues.Count == 0 ? GovernanceStatus.Compliant : issues[0];
	}

	/// <summary>True when the person has a given, not withdrawn, not expired consent on this image.</summary>
	public static bool HasActiveConsent(ImageGovernanceFacts f, Guid personId, DateOnly today) =>
		f.Consents.Any(c => c.PersonId == personId && IsActive(c, today));

	private static bool IsActive(ConsentFacts c, DateOnly today) =>
		c.GivenOn <= today
		&& (c.WithdrawnOn is null || c.WithdrawnOn > today)
		&& (c.ExpiresOn is null || c.ExpiresOn > today);
}
