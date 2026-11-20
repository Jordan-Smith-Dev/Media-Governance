using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Tests;

public class UsagePolicyTests
{
	[Theory]
	[InlineData(GovernanceStatus.Compliant, true)]
	[InlineData(GovernanceStatus.ExpiringSoon, true)]
	[InlineData(GovernanceStatus.NeedsTagging, true)]
	[InlineData(GovernanceStatus.Expired, false)]
	[InlineData(GovernanceStatus.NoConsent, false)]
	public void Only_expired_and_no_consent_images_are_unusable(GovernanceStatus status, bool usable) =>
		Assert.Equal(usable, UsagePolicy.IsUsable(status));
}
