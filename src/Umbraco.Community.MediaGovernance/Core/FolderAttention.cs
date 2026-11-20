namespace Umbraco.Community.MediaGovernance.Core;

internal enum FolderAttention
{
	None,

	/// <summary>Contains images that are expiring soon or still need tagging.</summary>
	Warning,

	/// <summary>Contains images that are expired or lack consent, so cannot be used.</summary>
	Withheld
}
