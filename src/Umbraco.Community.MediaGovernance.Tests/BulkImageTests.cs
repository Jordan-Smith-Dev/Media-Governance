using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Images;

namespace Umbraco.Community.MediaGovernance.Tests;

public class BulkImageTests
{
	private readonly FakeImageRepository _images = new();
	private readonly FakePersonRepository _people = new();
	private readonly FakeAuditRepository _auditRepo = new();
	private ImageGovernanceService Sut() => new(_images, _people, new AuditService(_auditRepo));

	[Fact]
	public async Task No_people_is_applied_and_keeps_the_images_other_details()
	{
		var media = Guid.NewGuid();
		_images.Images[media] = new ImageRow { MediaKey = media, DateTaken = new DateTime(2024, 5, 6), CustomExpiry = new DateTime(2030, 1, 1) };

		var result = await Sut().BulkAsync([media], new BulkImageChange(true, null, false), "a");

		Assert.Equal([media], result.Updated);
		Assert.True(_images.Images[media].NoPeople);
		Assert.Equal(new DateTime(2024, 5, 6), _images.Images[media].DateTaken);
		Assert.Equal(new DateTime(2030, 1, 1), _images.Images[media].CustomExpiry);
		Assert.Contains(_auditRepo.Rows, r => r.Action == "image.updated" && r.MediaKey == media);
	}

	[Fact]
	public async Task No_people_skips_images_that_have_someone_tagged()
	{
		var tagged = Guid.NewGuid();
		var untagged = Guid.NewGuid();
		var person = Guid.NewGuid();
		_people.People[person] = new PersonRow { Id = person, Name = "Verna Burke" };
		await _images.SaveAsync(new ImageRow { MediaKey = tagged }, [person]);

		var result = await Sut().BulkAsync([tagged, untagged], new BulkImageChange(true, null, false), "a");

		Assert.Equal([untagged], result.Updated);
		Assert.Equal([tagged], result.Skipped);
		Assert.False(_images.Images[tagged].NoPeople);
		Assert.Equal([person], _images.Tags[tagged]);   // tags untouched
	}

	[Fact]
	public async Task Expiry_is_set_for_every_image_without_touching_tags()
	{
		var a = Guid.NewGuid();
		var b = Guid.NewGuid();
		var person = Guid.NewGuid();
		_people.People[person] = new PersonRow { Id = person, Name = "Tom Reyes" };
		await _images.SaveAsync(new ImageRow { MediaKey = a, NoPeople = false }, [person]);

		await Sut().BulkAsync([a, b], new BulkImageChange(false, new DateOnly(2031, 3, 4), false), "a");

		Assert.Equal(new DateTime(2031, 3, 4), _images.Images[a].CustomExpiry);
		Assert.Equal(new DateTime(2031, 3, 4), _images.Images[b].CustomExpiry);
		Assert.Equal([person], _images.Tags[a]);
	}

	[Fact]
	public async Task Clearing_the_expiry_removes_it()
	{
		var media = Guid.NewGuid();
		_images.Images[media] = new ImageRow { MediaKey = media, CustomExpiry = new DateTime(2030, 1, 1) };

		await Sut().BulkAsync([media], new BulkImageChange(false, null, true), "a");

		Assert.Null(_images.Images[media].CustomExpiry);
	}

	[Fact]
	public async Task Exactly_one_change_must_be_chosen()
	{
		var media = Guid.NewGuid();
		await Assert.ThrowsAsync<ArgumentException>(() => Sut().BulkAsync([media], new BulkImageChange(false, null, false), "a"));
		await Assert.ThrowsAsync<ArgumentException>(() => Sut().BulkAsync([media], new BulkImageChange(true, new DateOnly(2031, 1, 1), false), "a"));
		Assert.Empty(_images.Images);
	}

	[Fact]
	public async Task Needs_at_least_one_image_and_a_sensible_maximum()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => Sut().BulkAsync([], new BulkImageChange(true, null, false), "a"));
		var tooMany = Enumerable.Range(0, ImageGovernanceService.MaxBulk + 1).Select(_ => Guid.NewGuid());
		await Assert.ThrowsAsync<ArgumentException>(() => Sut().BulkAsync(tooMany, new BulkImageChange(true, null, false), "a"));
	}
}
