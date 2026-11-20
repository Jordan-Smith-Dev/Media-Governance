namespace Umbraco.Community.MediaGovernance.Services.Settings;

/// <summary>The settings an admin can change in the CMS when admin settings are switched on.</summary>
public record AdminSettingsUpdate(
	bool BlockWithheldFiles,
	bool HideWithheldInPages,
	bool ShowStatusSigns,
	bool ColourTreeIcons,
	bool ShowFolderMarkers,
	bool ConsentDetails,
	bool ConsentExpiry,
	bool ConsentEvidence,
	IReadOnlyList<string> PermittedUses);
