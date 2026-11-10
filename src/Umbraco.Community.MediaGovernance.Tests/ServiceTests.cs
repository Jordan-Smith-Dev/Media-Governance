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
    public async Task OpenEvidence_returns_file_with_fixed_content_type_and_audits_the_view()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");
        _evidence.Files[_consents.Consents[row.Id].EvidencePath!] = [9, 8, 7];

        var file = await Sut().OpenEvidenceAsync(row.Id, "viewer-1");

        Assert.NotNull(file);
        Assert.Equal("application/pdf", file!.ContentType);
        Assert.EndsWith(".pdf", file.FileName);
        using var copy = new MemoryStream();
        await file.Content.CopyToAsync(copy);
        Assert.Equal(new byte[] { 9, 8, 7 }, copy.ToArray());
        Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.evidence.viewed" && r.MediaKey == _media && r.ActorKey == "viewer-1");
    }

    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    public async Task OpenEvidence_maps_extension_to_content_type(string ext, string type)
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        _evidence.Extension = ext;
        await Sut().AttachEvidenceAsync(row.Id, "x" + ext, File1(), "a");
        _evidence.Files[_consents.Consents[row.Id].EvidencePath!] = [1];
        Assert.Equal(type, (await Sut().OpenEvidenceAsync(row.Id, "a"))!.ContentType);
    }

    [Fact]
    public async Task OpenEvidence_is_null_when_no_evidence_or_file_missing_and_does_not_audit()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        Assert.Null(await Sut().OpenEvidenceAsync(row.Id, "a"));              // none attached

        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");        // attached, but file absent from store
        Assert.Null(await Sut().OpenEvidenceAsync(row.Id, "a"));
        Assert.DoesNotContain(_auditRepo.Rows, r => r.Action == "consent.evidence.viewed");
    }

    [Fact]
    public async Task RemoveEvidence_unlinks_deletes_the_file_and_audits()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");
        var path = _consents.Consents[row.Id].EvidencePath;

        await Sut().RemoveEvidenceAsync(row.Id, "remover-1");

        Assert.Null(_consents.Consents[row.Id].EvidencePath);
        Assert.Equal([path], _evidence.Deleted);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.evidence.removed" && r.ActorKey == "remover-1");
    }

    [Fact]
    public async Task RemoveEvidence_with_none_on_file_does_nothing()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");

        await Sut().RemoveEvidenceAsync(row.Id, "a");

        Assert.Empty(_evidence.Deleted);
        Assert.DoesNotContain(_auditRepo.Rows, r => r.Action == "consent.evidence.removed");
    }

    [Fact]
    public async Task RemoveEvidence_still_unlinks_when_the_file_cannot_be_deleted()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");
        _evidence.FailOnDelete = true;

        await Sut().RemoveEvidenceAsync(row.Id, "a");

        Assert.Null(_consents.Consents[row.Id].EvidencePath);
        Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.evidence.removed");
    }

    [Fact]
    public async Task RemoveEvidence_unknown_consent_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().RemoveEvidenceAsync(Guid.NewGuid(), "a"));

    [Fact]
    public async Task EvidenceInfo_reports_type_and_size_for_consents_with_evidence_only()
    {
        _images.Tags[_media] = [_person];
        var withFile = await Sut().AddAsync(_media, Input(), "a");
        var without = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(withFile.Id, "a.pdf", File1(), "a");
        _evidence.Files[_consents.Consents[withFile.Id].EvidencePath!] = new byte[2048];

        var info = await Sut().EvidenceInfoAsync([_consents.Consents[withFile.Id], _consents.Consents[without.Id]]);

        Assert.Equal(new EvidenceInfo("PDF", 2048), info[withFile.Id]);
        Assert.False(info.ContainsKey(without.Id));
    }

    [Fact]
    public async Task EvidenceInfo_size_is_null_when_the_file_is_missing()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "a");   // no bytes in the fake store

        var info = await Sut().EvidenceInfoAsync([_consents.Consents[row.Id]]);

        Assert.Null(info[row.Id].SizeBytes);
    }

    [Fact]
    public async Task EvidenceHistory_lists_uploads_views_and_removals_newest_first_and_ignores_other_entries()
    {
        _images.Tags[_media] = [_person];
        var row = await Sut().AddAsync(_media, Input(), "a");            // consent.added: not evidence history
        await Sut().AttachEvidenceAsync(row.Id, "a.pdf", File1(), "uploader");
        _evidence.Files[_consents.Consents[row.Id].EvidencePath!] = [1];
        await Sut().OpenEvidenceAsync(row.Id, "viewer");
        await Sut().RemoveEvidenceAsync(row.Id, "remover");

        var history = await Sut().EvidenceHistoryAsync(row.Id);

        Assert.Equal(["consent.evidence.removed", "consent.evidence.viewed", "consent.evidence"], history.Select(h => h.Action).ToArray());
        Assert.Equal(["remover", "viewer", "uploader"], history.Select(h => h.ActorKey).ToArray());
    }

    [Fact]
    public async Task EvidenceHistory_excludes_other_people_on_the_same_image()
    {
        var other = Guid.NewGuid();
        _images.Tags[_media] = [_person, other];
        var mine = await Sut().AddAsync(_media, Input(), "a");
        var theirs = await Sut().AddAsync(_media, Input() with { PersonId = other }, "a");
        await Sut().AttachEvidenceAsync(theirs.Id, "a.pdf", File1(), "someone-else");

        Assert.Empty(await Sut().EvidenceHistoryAsync(mine.Id));
    }

    [Fact]
    public async Task EvidenceHistory_unknown_consent_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().EvidenceHistoryAsync(Guid.NewGuid()));

    [Fact]
    public async Task OpenEvidence_unknown_consent_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().OpenEvidenceAsync(Guid.NewGuid(), "a"));

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
    private static MemoryStream Pdf(int pad = 0) => new([.. "%PDF-1.4"u8, .. new byte[pad]]);
    private static MemoryStream Png() => new([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
    private static MemoryStream Jpg() => new([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]);

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task Save_stores_under_person_folder_and_ignores_client_filename_path()
    {
        var person = Guid.NewGuid();
        var path = await Sut().SaveAsync(person, Guid.NewGuid(), @"..\..\evil.pdf", Pdf());
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
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), name, Pdf()));

    [Fact]
    public async Task Save_rejects_files_over_10MB() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "big.pdf", Pdf(10 * 1024 * 1024)));

    [Fact]
    public async Task DeleteForPerson_removes_the_whole_folder_and_nothing_else()
    {
        var sut = Sut();
        var person = Guid.NewGuid();
        await sut.SaveAsync(person, Guid.NewGuid(), "a.pdf", Pdf());
        await sut.SaveAsync(person, Guid.NewGuid(), "b.png", Png());
        var other = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "c.pdf", Pdf());

        await sut.DeleteForPersonAsync(person);

        Assert.False(Directory.Exists(Path.Combine(_root, person.ToString())));
        Assert.True(File.Exists(Path.Combine(_root, other)));
    }

    [Fact]
    public async Task Delete_removes_files_and_ignores_paths_outside_root()
    {
        var sut = Sut();
        var path = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "a.pdf", Pdf());
        var outside = Path.Combine(Path.GetTempPath(), "mg-outside-" + Guid.NewGuid() + ".txt");
        await File.WriteAllTextAsync(outside, "keep me", Encoding.UTF8);

        await sut.DeleteAsync([path, @"..\" + Path.GetFileName(outside)]);

        Assert.False(File.Exists(Path.Combine(_root, path)));
        Assert.True(File.Exists(outside));
        File.Delete(outside);
    }

    [Theory]
    [InlineData("form.pdf")]
    [InlineData("form.png")]
    [InlineData("form.jpg")]
    public async Task Save_rejects_content_that_does_not_match_the_extension(string name)
    {
        var html = new MemoryStream("<html><script>alert(1)</script></html>"u8.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), name, html));
    }

    [Fact]
    public async Task Save_rejects_a_real_png_named_as_pdf() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "x.pdf", Png()));

    [Theory]
    [InlineData("x.jpeg")]
    [InlineData("X.JPG")]
    public async Task Save_accepts_jpeg_content_under_either_jpeg_extension(string name) =>
        Assert.NotNull(await Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), name, Jpg()));

    [Fact]
    public async Task Save_rejection_message_does_not_leak_parameter_names()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "x.exe", Pdf()));
        Assert.DoesNotContain("Parameter", ex.Message);
    }

    [Fact]
    public async Task Stored_paths_use_forward_slashes()
    {
        var path = await Sut().SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "a.pdf", Pdf());
        Assert.DoesNotContain('\\', path);
        Assert.Contains('/', path);
    }

    [Fact]
    public async Task GetSize_returns_the_stored_length_and_null_for_missing_or_outside_paths()
    {
        var sut = Sut();
        var path = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "a.png", Png());

        Assert.Equal(Png().Length, await sut.GetSizeAsync(path));
        Assert.Null(await sut.GetSizeAsync("missing/file.png"));
        Assert.Null(await sut.GetSizeAsync("../outside.png"));
    }

    [Fact]
    public async Task OpenRead_returns_what_was_saved()
    {
        var sut = Sut();
        var path = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "a.png", Png());
        await using var stream = await sut.OpenReadAsync(path);
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal(Png().ToArray(), copy.ToArray());
    }

    [Fact]
    public async Task OpenRead_still_resolves_legacy_backslash_paths()
    {
        var sut = Sut();
        var path = await sut.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "a.pdf", Pdf());
        await using var stream = await sut.OpenReadAsync(path.Replace('/', '\\'));
        Assert.NotNull(stream);
    }

    [Theory]
    [InlineData("missing/file.pdf")]
    [InlineData("../outside.pdf")]
    [InlineData(@"..\outside.pdf")]
    public async Task OpenRead_returns_null_for_missing_or_outside_paths(string path) =>
        Assert.Null(await Sut().OpenReadAsync(path));
}
