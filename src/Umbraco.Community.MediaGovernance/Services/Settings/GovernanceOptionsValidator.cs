using System.Net.Mail;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.Settings;

internal static class GovernanceOptionsValidator
{
	public const int MaxRecipients = 25;
	public const int MaxUserGroups = 25;
	public const int MaxEmailLength = 254;

	/// <summary>Trims entries, drops blanks and removes duplicates (ignoring case), keeping the first spelling.</summary>
	public static SettingsUpdate Normalize(SettingsUpdate update) => update with
	{
		NotificationEmails = Clean(update.NotificationEmails ?? []),
		NotificationUserGroupIds = (update.NotificationUserGroupIds ?? []).Distinct().ToList()
	};

	private static List<string> Clean(IEnumerable<string> values) =>
		values.Select(v => v?.Trim() ?? string.Empty)
			.Where(v => v.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

	/// <summary>Throws <see cref="ArgumentException"/> when the settings cannot be saved. Call after <see cref="Normalize"/>.</summary>
	public static void Validate(SettingsUpdate update)
	{
		if (update.RetentionYears < 1 || update.RetentionYears > 100)
			throw new ArgumentException("Retention must be between 1 and 100 years.", nameof(update));
		if (update.WarningDays < 0 || update.WarningDays > 3650)
			throw new ArgumentException("Warning window must be between 0 and 3650 days.", nameof(update));

		if (update.NotificationEmails.Count > MaxRecipients)
			throw new ArgumentException($"Use at most {MaxRecipients} recipients.", nameof(update));
		if ((update.NotificationUserGroupIds?.Count ?? 0) > MaxUserGroups)
			throw new ArgumentException($"Choose at most {MaxUserGroups} user groups.", nameof(update));
		foreach (var email in update.NotificationEmails)
		{
			if (email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email.Trim())
				throw new ArgumentException($"'{email}' is not a valid email address.", nameof(update));
		}
	}
}
