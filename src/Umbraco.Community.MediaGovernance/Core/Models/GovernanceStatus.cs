using System.Text.Json.Serialization;

namespace Umbraco.Community.MediaGovernance.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GovernanceStatus
{
	Compliant,
	ExpiringSoon,
	Expired,
	NoConsent,
	NeedsTagging
}
