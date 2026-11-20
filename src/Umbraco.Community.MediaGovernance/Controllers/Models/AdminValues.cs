namespace Umbraco.Community.MediaGovernance.Controllers.Models;

/// <summary>The settings an admin can change. Sent only to admins, and only while admin settings are switched on.</summary>
internal record AdminValues(
	bool BlockWithheldFiles,
	bool HideWithheldInPages,
	bool ShowStatusSigns,
	bool ColourTreeIcons,
	bool ShowFolderMarkers,
	bool ConsentDetails,
	bool ConsentExpiry,
	bool ConsentEvidence,
	IReadOnlyList<string> PermittedUses);
