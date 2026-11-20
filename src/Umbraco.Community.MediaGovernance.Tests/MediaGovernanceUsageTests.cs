using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Status;

namespace Umbraco.Community.MediaGovernance.Tests;

public class MediaGovernanceUsageTests
{
	private static readonly DateOnly Today = new(2026, 10, 29);
	private readonly FakeImageRepository _images = new();

	private MediaGovernanceUsage Sut() =>
		new(new StatusLookupService(_images, new FakeSettings(), new FixedTimeProvider(Today)));

	private Guid Add(DateOnly? taken, bool noPeople, Guid[]? tagged = null)
	{
		var key = Guid.NewGuid();
		_images.Facts[key] = new(key, taken, null, null, noPeople, tagged ?? [], []);
		return key;
	}

	[Fact]
	public async Task Image_with_no_governance_data_is_usable_and_needs_tagging()
	{
		var key = Guid.NewGuid();
		Assert.Equal(GovernanceStatus.NeedsTagging, await Sut().GetStatusAsync(key));
		Assert.True(await Sut().IsUsableAsync(key));
	}

	private class FakeUploads : IMediaUploadDates
	{
		private readonly Dictionary<Guid, DateOnly> _dates;

		public FakeUploads(Dictionary<Guid, DateOnly> dates)
		{
			_dates = dates;
		}

		public Task<IReadOnlyDictionary<Guid, DateOnly>> GetAsync(IReadOnlyCollection<Guid> mediaKeys) =>
			Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(_dates);
	}

	[Fact]
	public async Task An_image_with_no_dates_uses_its_upload_date_for_retention()
	{
		var key = Add(null, noPeople: true);                         // no date taken or obtained
		var uploads = new FakeUploads(new() { [key] = new DateOnly(2020, 1, 1) });
		var sut = new MediaGovernanceUsage(new StatusLookupService(_images, new FakeSettings(), new FixedTimeProvider(Today), uploads));

		Assert.Equal(GovernanceStatus.Expired, await sut.GetStatusAsync(key));
		Assert.False(await sut.IsUsableAsync(key));
	}

	[Fact]
	public async Task Expired_image_is_not_usable()
	{
		var key = Add(new DateOnly(2020, 1, 1), noPeople: true);
		Assert.Equal(GovernanceStatus.Expired, await Sut().GetStatusAsync(key));
		Assert.False(await Sut().IsUsableAsync(key));
	}

	[Fact]
	public async Task Image_with_a_tagged_person_and_no_consent_is_not_usable()
	{
		var key = Add(new DateOnly(2025, 1, 1), noPeople: false, tagged: [Guid.NewGuid()]);
		Assert.False(await Sut().IsUsableAsync(key));
	}

	[Fact]
	public async Task Batch_lookup_returns_an_entry_for_every_key()
	{
		var good = Add(new DateOnly(2025, 1, 1), noPeople: true);
		var expired = Add(new DateOnly(2020, 1, 1), noPeople: true);
		var untouched = Guid.NewGuid();

		var map = await Sut().GetUsableAsync([good, expired, untouched]);

		Assert.True(map[good]);
		Assert.False(map[expired]);
		Assert.True(map[untouched]);
		Assert.Equal(3, map.Count);
	}

	[Fact]
	public async Task Batch_lookup_tolerates_duplicate_keys()
	{
		var key = Add(new DateOnly(2025, 1, 1), noPeople: true);
		var map = await Sut().GetUsableAsync([key, key]);
		Assert.True(map[key]);
		Assert.Single(map);
	}

	[Fact]
	public async Task Empty_batch_returns_empty_map() => Assert.Empty(await Sut().GetUsableAsync([]));
}
