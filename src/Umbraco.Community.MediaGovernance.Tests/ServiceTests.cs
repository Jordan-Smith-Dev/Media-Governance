using System.Text;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class PersonServiceTests
{
    private readonly FakePersonRepository _people = new();
    private readonly FakeEvidenceStore _evidence = new();
    private readonly FakeAuditRepository _auditRepo = new();
    private PersonService Sut() => new(_people, _evidence, new AuditService(_auditRepo));

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
    public async Task Erase_deletes_returned_evidence_files()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        _people.EvidenceToReturnOnErase.AddRange(["p/c1.pdf", "p/c2.pdf"]);
        await Sut().EraseAsync(p.Id, "a");
        Assert.Equal(["p/c1.pdf", "p/c2.pdf"], _evidence.Deleted);
        Assert.Contains(p.Id, _people.Erased);
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
    public async Task Erase_still_audits_when_file_deletion_fails()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        _evidence.FailOnDelete = true;
        await Sut().EraseAsync(p.Id, "a");           // must not throw
        Assert.Contains(p.Id, _people.Erased);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "person.erased");
    }

    [Fact]
    public async Task Erase_removes_the_whole_evidence_folder_of_the_person()
    {
        var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
        await Sut().EraseAsync(p.Id, "a");
        Assert.Contains(p.Id, _evidence.PersonFoldersDeleted);
    }

    [Fact]
    public async Task Erase_unknown_person_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().EraseAsync(Guid.NewGuid(), "a"));
}

public class ConsentServiceTests
{
    private readonly FakeConsentRepository _consents = new();
    private readonly FakeImageRepository _images = new();
    private readonly FakeEvidenceStore _evidence = new();
    private readonly FakeAuditRepository _auditRepo = new();
    private readonly Guid _media = Guid.NewGuid();
    private readonly Guid _person = Guid.NewGuid();

    private ConsentService Sut() => new(_consents, _images, _evidence, new AuditService(_auditRepo), new FakeSettings());
    private ConsentInput Input() => new(_person, new DateOnly(2026, 1, 1), ConsentGivenBy.Self, ["Web", "Print"], null, null);

    [Fact]
    public async Task Add_requires_person_tagged_on_image() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().AddAsync(_media, Input(), "a"));

    [Fact]
    public async Task Add_stores_record_and_audits()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "actor-1");
        var stored = _consents.Consents[row.Id];
        Assert.Equal(_media, stored.MediaKey);
        Assert.Equal("Web,Print", stored.PermittedUses);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.added" && r.MediaKey == _media);
    }

    [Fact]
    public async Task Withdraw_sets_date_and_audits()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().WithdrawAsync(row.Id, new DateOnly(2026, 6, 1), "a");
        Assert.Equal(new DateTime(2026, 6, 1), _consents.Consents[row.Id].WithdrawnOn);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.withdrawn");
    }

    [Theory]
    [InlineData("Verna Burke")]
    [InlineData("a colleague")]
    [InlineData("")]
    public async Task Add_rejects_a_given_by_that_is_not_one_of_the_allowed_capacities(string givenBy)
    {
        _images.Tags[_media] = [_person];
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().AddAsync(_media, Input() with { GivenBy = givenBy }, "a"));
        Assert.Empty(_consents.Consents);
    }

    [Theory]
    [InlineData(ConsentGivenBy.Self)]
    [InlineData(ConsentGivenBy.ParentOrGuardian)]
    [InlineData(ConsentGivenBy.LegalRepresentative)]
    public async Task Add_accepts_every_allowed_capacity(string givenBy)
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input() with { GivenBy = givenBy }, "a");
        Assert.Equal(givenBy, row.GivenBy);
    }

    [Fact]
    public async Task Add_requires_at_least_one_permitted_use()
    {
        _images.Tags[_media] = [_person];
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().AddAsync(_media, Input() with { PermittedUses = [] }, "a"));
    }

    [Fact]
    public async Task Add_rejects_a_permitted_use_that_is_not_in_the_configured_list()
    {
        _images.Tags[_media] = [_person];
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Sut().AddAsync(_media, Input() with { PermittedUses = ["Web", "Billboards"] }, "a"));
    }

    private static MemoryStream File1() => new(new byte[] { 1, 2, 3 });

    [Fact]
    public async Task Replacing_evidence_keeps_old_file_when_new_upload_fails()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");
        var original = _consents.Consents[row.Id].EvidencePath;

        _evidence.FailOnSave = true;
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().AttachEvidenceAsync(row.Id, "b.docx", File1(), "a"));

        Assert.Equal(original, _consents.Consents[row.Id].EvidencePath);
        Assert.Empty(_evidence.Deleted);
    }

    [Fact]
    public async Task Replacing_evidence_deletes_old_file_only_when_path_changes()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");
        var oldPath = _consents.Consents[row.Id].EvidencePath;

        await Sut().AttachEvidenceAsync(row.Id, "same.pdf", File1(), "a");   // same path: overwritten, never deleted
        Assert.Empty(_evidence.Deleted);

        _evidence.Extension = ".png";
        await Sut().AttachEvidenceAsync(row.Id, "other.png", File1(), "a"); // new path: old file removed
        Assert.Equal([oldPath], _evidence.Deleted);
    }

    [Fact]
    public async Task Withdraw_unknown_consent_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().WithdrawAsync(Guid.NewGuid(), new DateOnly(2026, 6, 1), "a"));
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

public class EvidenceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-evidence-" + Guid.NewGuid());
    private EvidenceStore Sut() => new(_root);
    private static MemoryStream Bytes(int n) => new(new byte[n]);

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task Save_stores_under_person_folder_and_ignores_client_filename_path()
    {
        var person = Guid.NewGuid();
        var path = await Sut().SaveAsync(person, Guid.NewGuid(), @"..\..\evil.pdf", Bytes(10));
        var full = Path.GetFullPath(Path.Combine(_root, path));
        Assert.StartsWith(Path.GetFullPath(_root), full);
        Assert.StartsWith(person.ToString(), path);
        Assert.True(File.Exists(full));
    }

    [Theory]
    [InlineData("form.exe")]
    [InlineData("form.html")]
    [InlineData("form")]
    public async Task Save_rejects_disallowed_extensions(string name) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), name, Bytes(10)));

    [Fact]
    public async Task Save_rejects_files_over_10MB() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "big.pdf", Bytes(10 * 1024 * 1024 + 1)));

    [Fact]
    public async Task DeleteForPerson_removes_the_whole_folder_and_nothing_else()
    {
        var sut = Sut();
        var person = Guid.NewGuid();
        await sut.SaveAsync(person, Guid.NewGuid(), "a.pdf", Bytes(5));
        await sut.SaveAsync(person, Guid.NewGuid(), "b.png", Bytes(5));
        var other = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "c.pdf", Bytes(5));

        await sut.DeleteForPersonAsync(person);

        Assert.False(Directory.Exists(Path.Combine(_root, person.ToString())));
        Assert.True(File.Exists(Path.Combine(_root, other)));
    }

    [Fact]
    public async Task Delete_removes_files_and_ignores_paths_outside_root()
    {
        var sut = Sut();
        var path = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "a.pdf", Bytes(5));
        var outside = Path.Combine(Path.GetTempPath(), "mg-outside-" + Guid.NewGuid() + ".txt");
        await File.WriteAllTextAsync(outside, "keep me", Encoding.UTF8);

        await sut.DeleteAsync([path, @"..\" + Path.GetFileName(outside)]);

        Assert.False(File.Exists(Path.Combine(_root, path)));
        Assert.True(File.Exists(outside));
        File.Delete(outside);
    }
}
