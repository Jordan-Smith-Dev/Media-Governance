namespace Umbraco.Community.MediaGovernance.TagHelpers;

public enum UsageDisplay
{
	/// <summary>Render the content only when the image may be used (the default).</summary>
	Usable,

	/// <summary>Render the content only when the image may NOT be used, for a placeholder or message.</summary>
	Unusable
}
