namespace Umbraco.Community.MediaGovernance.Services.Settings;

/// <summary>What an admin changed in the CMS, only for values that differ from appsettings. Null means "follow appsettings".</summary>
internal record AdminOverrides(
	bool? BlockWithheldFiles,
	bool? HideWithheldInPages,
	bool? ShowStatusSigns,
	bool? ColourTreeIcons,
	bool? ShowFolderMarkers,
	bool? ConsentDetails,
	bool? ConsentExpiry,
	bool? ConsentEvidence,
	IReadOnlyList<string>? PermittedUses)
{
	public bool IsEmpty => this is
	{
		BlockWithheldFiles: null, HideWithheldInPages: null, ShowStatusSigns: null, ColourTreeIcons: null,
		ShowFolderMarkers: null, ConsentDetails: null, ConsentExpiry: null, ConsentEvidence: null, PermittedUses: null
	};
}
