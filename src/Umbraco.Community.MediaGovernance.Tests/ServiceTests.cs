using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class PersonServiceTests
{
    private readonly FakePersonRepository _people = new();
    private readonly FakeAuditRepository _auditRepo = new();
    private PersonService Sut() => new(_people, new AuditService(_auditRepo), new FakeSettings(), new EvidenceStore(new FakeEvidenceFiles()), new FixedTimeProvider(new DateOnly(2026, 11, 17)));

    [Fact]
    public async Task Create_requires_a_name() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Sut().CreateAsync(new PersonInput("  ", null, null, null, null), "actor"));

    [Fact]
    public async Task Create_and_update_keep_a_trimmed_job_title_and_treat_blank_as_none()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null, "  Lecturer "), "a");
        Assert.Equal("Lecturer", _people.People[p.Id].JobTitle);

        await Sut().UpdateAsync(p.Id, new PersonInput("Tom Reyes", null, null, null, null, "   "), "a");
        Assert.Null(_people.People[p.Id].JobTitle);
    }

    [Fact]
    public async Task Create_saves_person_and_audits()
    {
        var p = await Sut().CreateAsync(new PersonInput("Verna Burke", "verna@example.com", null, Guid.NewGuid(), null), "actor-1");
        Assert.Equal("Verna Burke", _people.People[p.Id].Name);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "person.created" && r.ActorKey == "actor-1" && r.PersonId == p.Id);
    }

    [Fact]
    public async Task Erase_writes_anonymised_audit_entry()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", "tom@example.com", "0123", null, null), "a");
        await Sut().EraseAsync(p.Id, "a");
        var entry = Assert.Single(_auditRepo.Rows, r => r.Action == "person.erased");
        Assert.DoesNotContain("Tom", entry.Detail ?? "");
        Assert.DoesNotContain("tom@example.com", entry.Detail ?? "");
        // earlier audit rows about the person must not retain personal data either
        Assert.All(_auditRepo.Rows, r => Assert.DoesNotContain("tom@example.com", r.Detail ?? ""));
        Assert.All(_auditRepo.Rows, r => Assert.DoesNotContain("Tom Reyes", r.Detail ?? ""));
    }

    [Fact]
    public async Task Erase_unknown_person_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().EraseAsync(Guid.NewGuid(), "a"));

    [Fact]
    public async Task Erase_removes_the_person()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        await Sut().EraseAsync(p.Id, "a");
        Assert.Contains(p.Id, _people.Erased);
    }

    [Fact]
    public async Task A_new_person_has_not_given_consent()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        Assert.False(p.ConsentGiven);
        Assert.Null(p.ConsentUpdatedUtc);
    }

    [Fact]
    public async Task Giving_consent_is_recorded_and_audited_with_who_and_when()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        var updated = await Sut().SetConsentAsync(p.Id, true, "editor-1");
        Assert.True(updated.ConsentGiven);
        Assert.NotNull(updated.ConsentUpdatedUtc);
        var entry = Assert.Single(_auditRepo.Rows, r => r.Action == "consent.given");
        Assert.Equal("editor-1", entry.ActorKey);
        Assert.Equal(p.Id, entry.PersonId);
        Assert.Null(entry.MediaKey);
    }

    [Fact]
    public async Task Withdrawing_consent_is_audited()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        await Sut().SetConsentAsync(p.Id, true, "a");
        var updated = await Sut().SetConsentAsync(p.Id, false, "b");
        Assert.False(updated.ConsentGiven);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.withdrawn" && r.ActorKey == "b");
    }

    [Fact]
    public async Task Setting_consent_to_what_it_already_is_changes_and_audits_nothing()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        await Sut().SetConsentAsync(p.Id, false, "a");
        Assert.DoesNotContain(_auditRepo.Rows, r => r.Action.StartsWith("consent."));
    }

    [Fact]
    public async Task Consent_for_an_unknown_person_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().SetConsentAsync(Guid.NewGuid(), true, "a"));
}

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
