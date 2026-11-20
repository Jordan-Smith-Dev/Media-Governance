using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Overview;

namespace Umbraco.Community.MediaGovernance.Tests;

public class ExportServiceTests
{
	private static OverviewItem Item(string name) =>
		new(Guid.NewGuid(), name, "Campus", ["A, B"], new DateOnly(2025, 1, 2), new DateOnly(2028, 1, 2), 800, "Given", GovernanceStatus.Compliant);

	[Fact]
	public void Csv_has_header_and_escapes_commas_and_quotes()
	{
		var csv = ExportService.ToCsv([Item("Say \"hi\", ok")]);
		var lines = csv.TrimEnd().Split("\r\n");
		Assert.Equal("Name,Folder,People,Date taken,Expiry date,Consent,Status", lines[0]);
		Assert.Equal("\"Say \"\"hi\"\", ok\",Campus,\"A, B\",2025-01-02,2028-01-02,Given,Compliant", lines[1]);
	}

	[Fact]
	public void Csv_says_No_people_when_the_image_is_marked_as_having_none()
	{
		var item = Item("Library") with { People = [], NoPeople = true };
		var line = ExportService.ToCsv([item]).TrimEnd().Split("\r\n")[1];
		Assert.Equal("Library,Campus,No people,2025-01-02,2028-01-02,Given,Compliant", line);
	}

	[Fact]
	public void Csv_says_Not_tagged_yet_when_nobody_is_tagged_and_it_is_not_marked_as_having_none()
	{
		var item = Item("Sports day") with { People = [], NoPeople = false };
		var line = ExportService.ToCsv([item]).TrimEnd().Split("\r\n")[1];
		Assert.Equal("Sports day,Campus,Not tagged yet,2025-01-02,2028-01-02,Given,Compliant", line);
	}

	[Fact]
	public async Task Overview_items_say_whether_the_image_is_marked_as_having_no_people()
	{
		var images = new FakeImageRepository();
		var marked = new ImageInfo(Guid.NewGuid(), "Marked", "F");
		var unmarked = new ImageInfo(Guid.NewGuid(), "Unmarked", "F");
		images.Facts[marked.Key] = new(marked.Key, new(2025, 1, 1), null, null, true, [], []);
		images.Facts[unmarked.Key] = new(unmarked.Key, new(2025, 1, 1), null, null, false, [], []);
		var sut = new OverviewService(new FakeCatalog(marked, unmarked), images, new FakePersonRepository(), new FakeSettings(),
			new FixedTimeProvider(new DateOnly(2026, 10, 29)));

		var items = (await sut.QueryAsync(new OverviewQuery(null, null, 0, 50))).Items;

		Assert.True(items.Single(i => i.Name == "Marked").NoPeople);
		Assert.False(items.Single(i => i.Name == "Unmarked").NoPeople);
	}

	[Theory]
	[InlineData("=SUM(A1)")]
	[InlineData("+1")]
	[InlineData("-1")]
	[InlineData("@cmd")]
	public void Csv_neutralises_formula_injection(string name)
	{
		var csv = ExportService.ToCsv([Item(name)]);
		Assert.Contains("'" + name, csv);
	}
}
