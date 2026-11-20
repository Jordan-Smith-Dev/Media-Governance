using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.Settings;

/// <summary>What is in force: appsettings, with the retention period, warning window and recipients replaced by anything saved in the CMS.</summary>
public record GovernanceOptions(
	int RetentionYears,
	int WarningDays,
	IReadOnlyList<string> NotificationEmails,
	IReadOnlyList<Guid>? NotificationUserGroupIds = null,
	bool BlockWithheldFiles = true,
	bool HideWithheldInPages = true,
	bool ShowStatusSigns = false,
	bool ColourTreeIcons = false,
	bool ShowFolderMarkers = false,
	bool ConsentDetails = false,
	bool ConsentExpiry = false,
	bool ConsentEvidence = false,
	IReadOnlyList<string>? PermittedUses = null)
{
	public GovernanceSettings ToCore() => new(RetentionYears, WarningDays);

	/// <summary>True when consents are recorded per use (a list of permitted uses is configured).</summary>
	public bool UsesOn => PermittedUses is { Count: > 0 };

	public static GovernanceOptions Default { get; } = new(3, 90, []);
}
