using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Overview;

namespace Umbraco.Community.MediaGovernance.Tests;

public class OverviewServiceTests
{
	private static readonly DateOnly Today = new(2026, 10, 29);
	private readonly Guid _verna = Guid.NewGuid();
	private readonly ImageInfo _compliant = new(Guid.NewGuid(), "Library exterior", "Campus");
	private readonly ImageInfo _untagged = new(Guid.NewGuid(), "Welcome week", "Events");
	private readonly ImageInfo _noConsent = new(Guid.NewGuid(), "Team offsite", "Staff");
	private readonly ImageInfo _expired = new(Guid.NewGuid(), "Campus aerial", "Campus");

	private OverviewService Sut()
	{
		var images = new FakeImageRepository();
		images.Facts[_compliant.Key] = new(_compliant.Key, new(2025, 1, 1), null, null, true, [], []);
		images.Facts[_untagged.Key] = new(_untagged.Key, new(2025, 1, 1), null, null, false, [], []);
		images.Facts[_noConsent.Key] = new(_noConsent.Key, new(2025, 1, 1), null, null, false, [_verna], []);
		images.Facts[_expired.Key] = new(_expired.Key, new(2020, 1, 1), null, null, true, [], []);

		var people = new FakePersonRepository();
		people.People[_verna] = new PersonRow { Id = _verna, Name = "Verna Burke" };

		return new OverviewService(
			new FakeCatalog(_compliant, _untagged, _noConsent, _expired),
			images, people, new FakeSettings(), new FixedTimeProvider(Today));
	}

	private static string[] Names(OverviewPage page) => page.Items.Select(i => i.Name).ToArray();

	[Fact]
	public async Task Sorts_by_status_most_urgent_first()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Status));
		Assert.Equal(["Campus aerial", "Team offsite", "Welcome week", "Library exterior"], Names(page));
	}

	[Fact]
	public async Task Sorts_by_status_descending_reverses_it()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Status, true));
		Assert.Equal(["Library exterior", "Welcome week", "Team offsite", "Campus aerial"], Names(page));
	}

	[Fact]
	public async Task Sorts_by_expiry_with_ties_broken_by_name()
	{
		var asc = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Expiry));
		Assert.Equal(["Campus aerial", "Library exterior", "Team offsite", "Welcome week"], Names(asc));

		var desc = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Expiry, true));
		Assert.Equal(["Library exterior", "Team offsite", "Welcome week", "Campus aerial"], Names(desc));
	}

	[Fact]
	public async Task Sorts_by_folder_then_name()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Folder));
		Assert.Equal(["Campus aerial", "Library exterior", "Welcome week", "Team offsite"], Names(page));
	}

	[Fact]
	public async Task Sorts_by_name_descending()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Name, true));
		Assert.Equal(["Welcome week", "Team offsite", "Library exterior", "Campus aerial"], Names(page));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Images_without_a_date_sort_last_in_either_direction(bool descending)
	{
		var dated = new ImageInfo(Guid.NewGuid(), "Dated", "A");
		var undated = new ImageInfo(Guid.NewGuid(), "Undated", "A");
		var images = new FakeImageRepository();
		images.Facts[dated.Key] = new(dated.Key, new(2025, 1, 1), null, null, true, [], []);
		images.Facts[undated.Key] = new(undated.Key, null, null, null, true, [], []);
		var sut = new OverviewService(new FakeCatalog(undated, dated), images, new FakePersonRepository(), new FakeSettings(), new FixedTimeProvider(Today));

		var page = await sut.QueryAsync(new OverviewQuery(null, null, 0, 50, null, OverviewSort.Expiry, descending));

		Assert.Equal("Undated", page.Items[^1].Name);
	}

	[Fact]
	public async Task Counts_cover_all_images_regardless_of_filter()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(GovernanceStatus.Expired, "campus aerial", 0, 50));
		Assert.Equal(4, page.AllCount);
		Assert.Equal(1, page.Counts[GovernanceStatus.Expired]);
		Assert.Equal(1, page.Counts[GovernanceStatus.NoConsent]);
		Assert.Equal(1, page.Counts[GovernanceStatus.NeedsTagging]);
		Assert.Equal(1, page.Counts[GovernanceStatus.Compliant]);
	}

	[Fact]
	public async Task Status_filter_matches_StatusCalculator()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(GovernanceStatus.NoConsent, null, 0, 50));
		var item = Assert.Single(page.Items);
		Assert.Equal(_noConsent.Key, item.MediaKey);
		Assert.Equal("Not given", item.Consent);
		Assert.Equal(["Verna Burke"], item.People);
	}

	[Fact]
	public async Task Paging_returns_requested_slice_ordered_by_name()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, null, 1, 2));
		Assert.Equal(4, page.Total);
		Assert.Equal(["Library exterior", "Team offsite"], page.Items.Select(i => i.Name));
	}

	[Theory]
	[InlineData("events", "Welcome week")]      // folder
	[InlineData("TEAM", "Team offsite")]        // name, case-insensitive
	[InlineData("verna", "Team offsite")]       // person
	public async Task Text_filter_matches_name_folder_or_person(string filter, string expectedName)
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, filter, 0, 50));
		Assert.Equal(expectedName, Assert.Single(page.Items).Name);
	}

	[Fact]
	public async Task Consent_column_reflects_consent_even_when_image_is_Expired()
	{
		var old = new ImageInfo(Guid.NewGuid(), "Old tagged", "Campus");
		var images = new FakeImageRepository();
		images.Facts[old.Key] = new(old.Key, new(2020, 1, 1), null, null, false, [_verna], []);   // Expired AND no consent
		var people = new FakePersonRepository();
		people.People[_verna] = new PersonRow { Id = _verna, Name = "Verna Burke" };
		var sut = new OverviewService(new FakeCatalog(old), images, people, new FakeSettings(), new FixedTimeProvider(Today));

		var item = Assert.Single((await sut.QueryAsync(new OverviewQuery(null, null, 0, 50))).Items);

		Assert.Equal(GovernanceStatus.Expired, item.Status);
		Assert.Equal("Not given", item.Consent);
	}

	[Fact]
	public async Task Query_can_be_limited_to_chosen_images()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(null, null, 0, 50, [_compliant.Key, _expired.Key]));
		Assert.Equal(["Campus aerial", "Library exterior"], page.Items.Select(i => i.Name));
		Assert.Equal(4, page.AllCount);   // the tile counts still describe the whole library
	}

	[Fact]
	public async Task Expiry_and_days_are_reported()
	{
		var page = await Sut().QueryAsync(new OverviewQuery(GovernanceStatus.Compliant, null, 0, 50));
		var item = Assert.Single(page.Items);
		Assert.Equal(new DateOnly(2028, 1, 1), item.Expiry);
		Assert.Equal(new DateOnly(2028, 1, 1).DayNumber - Today.DayNumber, item.DaysToExpiry);
	}

	[Fact]
	public async Task StatusMap_returns_every_image()
	{
		var map = await Sut().StatusMapAsync();
		Assert.Equal(GovernanceStatus.Expired, map[_expired.Key]);
		Assert.Equal(4, map.Count);
	}
}
