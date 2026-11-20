namespace Umbraco.Community.MediaGovernance.Controllers.Models;

/// <summary>The settings screen: what is in force, the appsettings defaults behind retention and the warning window, and the read-only values.</summary>
internal record SettingsDto(
	int RetentionYears,
	int WarningDays,
	int DefaultRetentionYears,
	int DefaultWarningDays,
	IReadOnlyList<string> NotificationEmails,
	IReadOnlyList<Guid> NotificationUserGroupIds,
	bool BlockWithheldFiles,
	bool HideWithheldInPages,
	IReadOnlyList<string> AllowedUserGroups,
	bool ShowStatusSigns = false,
	bool ShowFolderMarkers = false,
	bool ConsentDetails = false,
	bool ConsentExpiry = false,
	bool ConsentEvidence = false,
	IReadOnlyList<string>? PermittedUses = null,
	string? EvidenceLocation = null,
	AdminSettingsDto? AdminSettings = null);
