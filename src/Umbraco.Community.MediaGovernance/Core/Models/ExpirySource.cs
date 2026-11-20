using System.Text.Json.Serialization;

namespace Umbraco.Community.MediaGovernance.Core.Models;

/// <summary>Where an image's effective expiry date comes from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpirySource
{
	/// <summary>No date taken or obtained, and no custom expiry, so the image does not expire.</summary>
	None,

	/// <summary>Date taken (or obtained) plus the default retention period.</summary>
	Default,

	/// <summary>A custom expiry date set on the image.</summary>
	Custom,

	/// <summary>A person's consent ends before the retention period would.</summary>
	Consent
}
