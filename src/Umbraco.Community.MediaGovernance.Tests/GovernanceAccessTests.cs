using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Tests;

public class GovernanceAccessTests
{
	[Fact]
	public void With_no_groups_configured_only_sensitive_data_has_access() =>
		Assert.Equal(["sensitiveData"], GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions()));

	[Fact]
	public void Configured_groups_replace_the_default() =>
		Assert.Equal(["admin", "editors"], GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions { AllowedUserGroups = ["admin", " editors "] }));

	[Fact]
	public void Blank_entries_are_ignored_and_an_all_blank_list_is_still_restricted()
	{
		Assert.Equal(["admin"], GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions { AllowedUserGroups = ["", "admin"] }));
		Assert.Empty(GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions { AllowedUserGroups = [" "] }));
	}

	[Fact]
	public void Nobody_is_allowed_when_there_is_no_user() =>
		Assert.False(new GovernanceAccessChecker(Options.Create(new MediaGovernanceOptions())).IsAllowed(null));
}
