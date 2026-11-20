using Umbraco.Community.MediaGovernance.Services.Notifications;
using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Tests;

public class NotificationRecipientTests
{
	[Fact]
	public void Merges_typed_and_group_addresses_without_repeats() =>
		Assert.Equal(["a@x.com", "B@x.com", "c@x.com"],
			NotificationService.Merge(["a@x.com", "B@x.com"], ["b@x.com", " c@x.com ", "A@X.com", ""]));

	[Fact]
	public void Group_ids_are_deduplicated_and_defaulted()
	{
		var id = Guid.NewGuid();
		var cleaned = GovernanceOptionsValidator.Normalize(new(3, 90, [], [id, id]));
		Assert.Equal([id], cleaned.NotificationUserGroupIds);
		Assert.Empty(GovernanceOptionsValidator.Normalize(new SettingsUpdate(3, 90, [])).NotificationUserGroupIds!);
	}

	[Fact]
	public void Rejects_too_many_groups()
	{
		var many = Enumerable.Range(0, GovernanceOptionsValidator.MaxUserGroups + 1).Select(_ => Guid.NewGuid()).ToList();
		Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(new SettingsUpdate(3, 90, [], many)));
	}
}
