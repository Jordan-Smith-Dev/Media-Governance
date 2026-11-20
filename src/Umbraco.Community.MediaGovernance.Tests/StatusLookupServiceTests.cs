using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services.Status;

namespace Umbraco.Community.MediaGovernance.Tests;

public class StatusLookupServiceTests
{
	[Fact]
	public async Task Returns_status_only_for_requested_keys()
	{
		var wanted = Guid.NewGuid();
		var other = Guid.NewGuid();
		var images = new FakeImageRepository();
		images.Facts[wanted] = new(wanted, new(2020, 1, 1), null, null, true, [], []);   // expired
		images.Facts[other] = new(other, new(2025, 1, 1), null, null, true, [], []);

		var sut = new StatusLookupService(images, new FakeSettings(), new FixedTimeProvider(new DateOnly(2026, 10, 29)));
		var map = await sut.GetStatusesAsync([wanted]);

		Assert.Equal(GovernanceStatus.Expired, Assert.Single(map).Value);
		Assert.Equal(wanted, map.Keys.Single());
	}

	[Fact]
	public async Task Empty_request_returns_empty_map()
	{
		var sut = new StatusLookupService(new FakeImageRepository(), new FakeSettings(), new FixedTimeProvider(new DateOnly(2026, 10, 29)));
		Assert.Empty(await sut.GetStatusesAsync([]));
	}
}
