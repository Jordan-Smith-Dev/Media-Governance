using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Images;

namespace Umbraco.Community.MediaGovernance.Tests;

public class ImageGovernanceServiceTests
{
	private readonly FakeImageRepository _images = new();
	private readonly FakePersonRepository _people = new();
	private readonly FakeAuditRepository _auditRepo = new();

	[Fact]
	public async Task Save_rejects_tagging_a_person_that_does_not_exist()
	{
		var sut = new ImageGovernanceService(_images, _people, new AuditService(_auditRepo));
		await Assert.ThrowsAsync<ArgumentException>(() => sut.SaveAsync(Guid.NewGuid(),
			new ImageGovernanceUpdate(null, null, null, false, [Guid.NewGuid()]), "a"));
		Assert.Empty(_images.Images);
	}

	[Fact]
	public async Task Save_stores_dates_flag_and_tags_and_audits()
	{
		var media = Guid.NewGuid();
		var person = Guid.NewGuid();
		_people.People[person] = new PersonRow { Id = person, Name = "Verna Burke" };
		var sut = new ImageGovernanceService(_images, _people, new AuditService(_auditRepo));

		await sut.SaveAsync(media,
			new ImageGovernanceUpdate(new DateOnly(2025, 1, 2), null, new DateOnly(2030, 1, 1), false, [person]), "a");

		Assert.Equal(new DateTime(2025, 1, 2), _images.Images[media].DateTaken);
		Assert.Equal(new DateTime(2030, 1, 1), _images.Images[media].CustomExpiry);
		Assert.Equal([person], _images.Tags[media]);
		Assert.Contains(_auditRepo.Rows, r => r.Action == "image.updated" && r.MediaKey == media);
	}
}
