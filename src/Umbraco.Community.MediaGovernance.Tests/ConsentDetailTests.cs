using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeEvidenceFiles : IEvidenceFiles
{
    public Dictionary<string, byte[]> Files { get; } = new();
    public List<string> FoldersDeleted { get; } = new();
    public bool FailOnDeleteFolder { get; set; }

    public Task WriteAsync(string path, byte[] data) { Files[path] = data; return Task.CompletedTask; }
    public Task<Stream?> OpenReadAsync(string path) => Task.FromResult<Stream?>(Files.TryGetValue(path, out var b) ? new MemoryStream(b) : null);
    public Task<long?> SizeAsync(string path) => Task.FromResult(Files.TryGetValue(path, out var b) ? (long?)b.Length : null);
    public Task DeleteAsync(string path) { Files.Remove(path); return Task.CompletedTask; }
    public Task DeleteFolderAsync(string folder)
    {
        if (FailOnDeleteFolder) throw new IOException("locked");
        FoldersDeleted.Add(folder);
        foreach (var k in Files.Keys.Where(k => k.StartsWith(folder + "/")).ToList()) Files.Remove(k);
        return Task.CompletedTask;
    }
}

internal static class Samples
{
    public static byte[] Pdf(int extra = 0) => [.. "%PDF-1.4 test"u8.ToArray(), .. new byte[extra]];
    public static byte[] Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    public static byte[] Jpg => [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
}

/// <summary>Consent detail is optional: everything below applies only while its option is on.</summary>
public class ConsentDetailTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    private readonly FakePersonRepository _people = new();
    private readonly FakeAuditRepository _audit = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeEvidenceFiles _files = new();

    private PersonService Sut() => new(_people, new AuditService(_audit), _settings, new EvidenceStore(_files), new FixedTimeProvider(Today));

    private async Task<PersonRow> PersonAsync() => await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");

    private void On(bool details = false, bool expiry = false, bool evidence = false, params string[] uses) =>
        _settings.Options = _settings.Options with { ConsentDetails = details, ConsentExpiry = expiry, ConsentEvidence = evidence, PermittedUses = uses };

    // ---------------------------------------------------------------- defaults

    [Fact]
    public void Every_option_is_off_by_default_and_the_evidence_folder_is_private_by_name()
    {
        var o = new MediaGovernanceOptions();
        Assert.False(o.ConsentDetails);
        Assert.False(o.ConsentExpiry);
        Assert.False(o.ConsentEvidence);
        Assert.Empty(o.PermittedUses);
        Assert.Equal("governance-evidence", o.EvidenceFolder);
        Assert.Null(o.EvidenceStoragePath);
    }

    [Fact]
    public async Task With_everything_off_detail_in_the_request_is_ignored()
    {
        var p = await PersonAsync();
        var row = await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2020, 1, 1), "ParentOrGuardian", new(2030, 1, 1), ["Web"]), "a");
        Assert.True(row.ConsentGiven);
        Assert.Null(row.ConsentGivenOn);
        Assert.Null(row.ConsentGivenBy);
        Assert.Null(row.ConsentExpiresOn);
        Assert.Null(row.ConsentPermittedUses);
    }

    // ---------------------------------------------------------------- given on and given by

    [Fact]
    public async Task Details_default_to_today_and_the_person_themselves()
    {
        On(details: true);
        var p = await PersonAsync();
        var row = await Sut().SetConsentAsync(p.Id, new ConsentChange(true), "a");
        Assert.Equal(Today.ToDateTime(TimeOnly.MinValue), row.ConsentGivenOn);
        Assert.Equal(ConsentGivenBy.Self, row.ConsentGivenBy);
    }

    [Fact]
    public async Task A_past_date_and_a_parent_can_be_recorded()
    {
        On(details: true);
        var p = await PersonAsync();
        var row = await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2025, 3, 4), ConsentGivenBy.ParentOrGuardian), "a");
        Assert.Equal(new DateTime(2025, 3, 4), row.ConsentGivenOn);
        Assert.Equal(ConsentGivenBy.ParentOrGuardian, row.ConsentGivenBy);
    }

    [Fact]
    public async Task Consent_cannot_be_given_in_the_future_or_by_someone_unlisted()
    {
        On(details: true);
        var p = await PersonAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetConsentAsync(p.Id, new ConsentChange(true, Today.AddDays(1)), "a"));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetConsentAsync(p.Id, new ConsentChange(true, null, "Somebody else"), "a"));
        Assert.False((await _people.GetAsync(p.Id))!.ConsentGiven);
    }

    // ---------------------------------------------------------------- expiry

    [Fact]
    public async Task An_expiry_is_stored_when_expiry_is_on_and_must_come_after_the_date_given()
    {
        On(details: true, expiry: true);
        var p = await PersonAsync();
        var row = await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2026, 1, 1), null, new(2027, 1, 1)), "a");
        Assert.Equal(new DateTime(2027, 1, 1), row.ConsentExpiresOn);
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2026, 6, 1), null, new(2026, 6, 1)), "a"));
    }

    [Fact]
    public void An_expired_consent_is_no_longer_in_force_but_a_plain_yes_always_is()
    {
        var given = new PersonRow { Id = Guid.NewGuid(), ConsentGiven = true, ConsentGivenOn = new DateTime(2025, 1, 1), ConsentExpiresOn = new DateTime(2026, 6, 1) };
        Assert.False(ConsentFactsFactory.InForce(given, detailsOn: true, expiryOn: true, Today));
        Assert.True(ConsentFactsFactory.Expired(given, expiryOn: true, Today));
        // Same person with expiry switched off: the stored date is kept but ignored.
        Assert.True(ConsentFactsFactory.InForce(given, detailsOn: true, expiryOn: false, Today));
        Assert.False(ConsentFactsFactory.Expired(given, expiryOn: false, Today));
        Assert.True(ConsentFactsFactory.InForce(new PersonRow { ConsentGiven = true }, false, false, Today));
        Assert.False(ConsentFactsFactory.InForce(new PersonRow { ConsentGiven = false }, true, true, Today));
    }

    [Fact]
    public void An_expired_consent_makes_the_images_expired_straight_away()
    {
        var person = new PersonRow { Id = Guid.NewGuid(), ConsentGiven = true, ConsentGivenOn = new DateTime(2025, 1, 1), ConsentExpiresOn = new DateTime(2026, 6, 1) };
        var facts = new ImageGovernanceFacts(Guid.NewGuid(), new(2026, 1, 1), null, null, false, [person.Id],
            [ConsentFactsFactory.Create(person, true, true, false)!]);
        var settings = new GovernanceSettings(3, 90);
        Assert.Equal(GovernanceStatus.Expired, StatusCalculator.Calculate(facts, settings, Today));
        Assert.Contains(GovernanceStatus.Expired, StatusCalculator.Issues(facts, settings, Today));
    }

    [Fact]
    public void Switching_expiry_off_brings_the_image_back_without_losing_the_date()
    {
        var person = new PersonRow { Id = Guid.NewGuid(), ConsentGiven = true, ConsentExpiresOn = new DateTime(2026, 6, 1) };
        var facts = new ImageGovernanceFacts(Guid.NewGuid(), new(2026, 1, 1), null, null, false, [person.Id],
            [ConsentFactsFactory.Create(person, false, false, false)!]);
        Assert.Equal(GovernanceStatus.Compliant, StatusCalculator.Calculate(facts, new GovernanceSettings(3, 90), Today));
        Assert.NotNull(person.ConsentExpiresOn);
    }

    // ---------------------------------------------------------------- permitted uses

    [Fact]
    public async Task Uses_must_be_chosen_from_the_configured_list_and_at_least_one_is_needed()
    {
        On(uses: ["Web", "Print"]);
        var p = await PersonAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetConsentAsync(p.Id, new ConsentChange(true), "a"));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetConsentAsync(p.Id, new ConsentChange(true, PermittedUses: ["Radio"]), "a"));
        var row = await Sut().SetConsentAsync(p.Id, new ConsentChange(true, PermittedUses: ["web", "Print", "WEB"]), "a");
        Assert.Equal("Web,Print", row.ConsentPermittedUses);   // spelling from the configured list, no repeats
    }

    [Fact]
    public void An_image_is_only_cleared_for_the_uses_everyone_pictured_agreed_to()
    {
        var tom = new PersonRow { Id = Guid.NewGuid(), ConsentGiven = true, ConsentPermittedUses = "Web,Print" };
        var ann = new PersonRow { Id = Guid.NewGuid(), ConsentGiven = true, ConsentPermittedUses = "Web" };
        var facts = new ImageGovernanceFacts(Guid.NewGuid(), new(2026, 1, 1), null, null, false, [tom.Id, ann.Id],
            [ConsentFactsFactory.Create(tom, false, false, true)!, ConsentFactsFactory.Create(ann, false, false, true)!]);
        Assert.Equal(["Web"], StatusCalculator.ClearedUses(facts, ["Web", "Print"], Today));
    }

    // ---------------------------------------------------------------- history

    [Fact]
    public async Task Giving_updating_and_withdrawing_are_each_audited_and_a_repeat_is_not()
    {
        On(details: true, expiry: true);
        var p = await PersonAsync();
        await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2026, 1, 1), null, new(2027, 1, 1)), "a");
        await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2026, 1, 1), null, new(2027, 1, 1)), "a");   // nothing changed
        await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2026, 1, 1), null, new(2028, 1, 1)), "b");   // renewed
        await Sut().SetConsentAsync(p.Id, new ConsentChange(false), "c");
        Assert.Equal(["consent.given", "consent.updated", "consent.withdrawn"], _audit.Rows.Where(r => r.Action.StartsWith("consent.")).Select(r => r.Action).ToArray());
    }

    [Fact]
    public async Task Withdrawing_keeps_the_detail_that_was_recorded()
    {
        On(details: true);
        var p = await PersonAsync();
        await Sut().SetConsentAsync(p.Id, new ConsentChange(true, new(2025, 5, 5)), "a");
        var row = await Sut().SetConsentAsync(p.Id, new ConsentChange(false), "a");
        Assert.False(row.ConsentGiven);
        Assert.Equal(new DateTime(2025, 5, 5), row.ConsentGivenOn);
    }

    // ---------------------------------------------------------------- evidence

    [Fact]
    public async Task Evidence_is_refused_until_the_option_is_switched_on()
    {
        var p = await PersonAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().AttachEvidenceAsync(p.Id, "form.pdf", new MemoryStream(Samples.Pdf()), "a"));
        Assert.Empty(_files.Files);
    }

    [Fact]
    public async Task Evidence_is_stored_under_the_person_with_a_name_we_choose_and_the_upload_is_audited()
    {
        On(evidence: true);
        var p = await PersonAsync();
        var row = await Sut().AttachEvidenceAsync(p.Id, "../../evil name.PDF", new MemoryStream(Samples.Pdf()), "a");
        Assert.Matches($@"^{p.Id}/[0-9a-f-]{{36}}\.pdf$", row.ConsentEvidencePath);
        Assert.Single(_files.Files);
        Assert.Contains(_audit.Rows, r => r.Action == "consent.evidence" && r.PersonId == p.Id);
    }

    [Fact]
    public async Task Replacing_evidence_removes_the_old_file_but_a_failed_upload_keeps_it()
    {
        On(evidence: true);
        var p = await PersonAsync();
        var first = (await Sut().AttachEvidenceAsync(p.Id, "a.pdf", new MemoryStream(Samples.Pdf()), "a")).ConsentEvidencePath!;
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().AttachEvidenceAsync(p.Id, "b.pdf", new MemoryStream([1, 2, 3]), "a"));   // not a real PDF
        Assert.Contains(first, _files.Files.Keys);
        var second = (await Sut().AttachEvidenceAsync(p.Id, "c.png", new MemoryStream(Samples.Png), "a")).ConsentEvidencePath!;
        Assert.DoesNotContain(first, _files.Files.Keys);
        Assert.Contains(second, _files.Files.Keys);
    }

    [Fact]
    public async Task Viewing_evidence_is_audited_and_returns_a_fixed_content_type()
    {
        On(evidence: true);
        var p = await PersonAsync();
        await Sut().AttachEvidenceAsync(p.Id, "x.jpg", new MemoryStream(Samples.Jpg), "a");
        await using var file = (await Sut().OpenEvidenceAsync(p.Id, "viewer"))!.Content;
        Assert.Contains(_audit.Rows, r => r.Action == "consent.evidence.viewed" && r.ActorKey == "viewer");
        var opened = await Sut().OpenEvidenceAsync(p.Id, "viewer");
        Assert.Equal("image/jpeg", opened!.ContentType);
        opened.Content.Dispose();
    }

    [Fact]
    public async Task Evidence_with_none_on_file_opens_as_nothing_and_does_not_audit()
    {
        On(evidence: true);
        var p = await PersonAsync();
        Assert.Null(await Sut().OpenEvidenceAsync(p.Id, "a"));
        Assert.DoesNotContain(_audit.Rows, r => r.Action == "consent.evidence.viewed");
    }

    [Fact]
    public async Task Removing_evidence_unlinks_deletes_the_file_and_audits()
    {
        On(evidence: true);
        var p = await PersonAsync();
        await Sut().AttachEvidenceAsync(p.Id, "x.pdf", new MemoryStream(Samples.Pdf()), "a");
        await Sut().RemoveEvidenceAsync(p.Id, "a");
        Assert.Null((await _people.GetAsync(p.Id))!.ConsentEvidencePath);
        Assert.Empty(_files.Files);
        Assert.Contains(_audit.Rows, r => r.Action == "consent.evidence.removed");
    }

    [Fact]
    public async Task The_evidence_history_lists_uploads_views_and_removals_newest_first_for_that_person_only()
    {
        On(evidence: true);
        var p = await PersonAsync();
        var other = await PersonAsync();
        await Sut().AttachEvidenceAsync(p.Id, "x.pdf", new MemoryStream(Samples.Pdf()), "u1");
        await Sut().AttachEvidenceAsync(other.Id, "x.pdf", new MemoryStream(Samples.Pdf()), "u9");
        (await Sut().OpenEvidenceAsync(p.Id, "u2"))!.Content.Dispose();
        await Sut().RemoveEvidenceAsync(p.Id, "u3");
        var history = await Sut().EvidenceHistoryAsync(p.Id);
        Assert.Equal(["consent.evidence.removed", "consent.evidence.viewed", "consent.evidence"], history.Select(h => h.Action).ToArray());
        Assert.DoesNotContain(history, h => h.ActorKey == "u9");
    }

    [Fact]
    public async Task Evidence_already_stored_is_kept_but_hidden_when_the_option_is_switched_off()
    {
        On(evidence: true);
        var p = await PersonAsync();
        var path = (await Sut().AttachEvidenceAsync(p.Id, "x.pdf", new MemoryStream(Samples.Pdf()), "a")).ConsentEvidencePath!;
        On(evidence: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().OpenEvidenceAsync(p.Id, "a"));
        Assert.Equal(path, (await _people.GetAsync(p.Id))!.ConsentEvidencePath);
        Assert.Contains(path, _files.Files.Keys);
    }

    [Fact]
    public async Task Deleting_a_person_deletes_their_evidence_even_if_the_option_is_off()
    {
        On(evidence: true);
        var p = await PersonAsync();
        await Sut().AttachEvidenceAsync(p.Id, "x.pdf", new MemoryStream(Samples.Pdf()), "a");
        On(evidence: false);
        await Sut().EraseAsync(p.Id, "a");
        Assert.Empty(_files.Files);
        Assert.Contains(p.Id.ToString(), _files.FoldersDeleted);
    }

    [Fact]
    public async Task A_locked_evidence_folder_does_not_stop_the_deletion_or_its_audit_entry()
    {
        var p = await PersonAsync();
        _files.FailOnDeleteFolder = true;
        await Sut().EraseAsync(p.Id, "a");
        Assert.Contains(p.Id, _people.Erased);
        Assert.Contains(_audit.Rows, r => r.Action == "person.erased");
    }

    // ---------------------------------------------------------------- the file checks

    [Theory]
    [InlineData("form.exe")]
    [InlineData("form")]
    [InlineData("form.pdf.html")]
    public async Task Only_pdf_png_and_jpg_are_accepted(string name) =>
        await Assert.ThrowsAsync<ArgumentException>(() => new EvidenceStore(_files).SaveAsync(Guid.NewGuid(), name, new MemoryStream(Samples.Pdf())));

    [Fact]
    public async Task A_real_png_named_as_a_pdf_is_refused()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new EvidenceStore(_files).SaveAsync(Guid.NewGuid(), "x.pdf", new MemoryStream(Samples.Png)));
        Assert.DoesNotContain("parameter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Files_over_10_MB_are_refused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new EvidenceStore(_files).SaveAsync(Guid.NewGuid(), "big.pdf", new MemoryStream(Samples.Pdf((int)EvidenceContent.MaxBytes))));
        Assert.Empty(_files.Files);
    }

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("a/b.pdf")]
    [InlineData("11111111-1111-1111-1111-111111111111/../x.pdf")]
    [InlineData("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.exe")]
    public async Task Paths_that_this_store_did_not_make_are_never_opened_or_deleted(string path)
    {
        _files.Files[path] = [1];
        var store = new EvidenceStore(_files);
        Assert.Null(await store.OpenReadAsync(path));
        Assert.Null(await store.GetSizeAsync(path));
        await store.DeleteAsync(path);
        Assert.Contains(path, _files.Files.Keys);
    }

    [Fact]
    public async Task The_disk_store_stays_inside_its_folder_and_deletes_a_whole_person()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-evidence-" + Guid.NewGuid());
        try
        {
            var store = new EvidenceStore(new DiskEvidenceFiles(root));
            var person = Guid.NewGuid();
            var path = await store.SaveAsync(person, "x.png", new MemoryStream(Samples.Png));
            Assert.True(File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(Samples.Png.Length, await store.GetSizeAsync(path));
            await using (var read = (await store.OpenReadAsync(path))!) Assert.Equal(Samples.Png.Length, read.Length);
            await store.DeleteForPersonAsync(person);
            Assert.False(Directory.Exists(Path.Combine(root, person.ToString())));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task The_disk_files_refuse_to_leave_their_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-evidence-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var files = new DiskEvidenceFiles(root);
            await Assert.ThrowsAsync<ArgumentException>(() => files.WriteAsync("../outside.pdf", [1]));
            Assert.Null(await files.OpenReadAsync("../outside.pdf"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // ---------------------------------------------------------------- the evidence folder is never served

    [Theory]
    [InlineData("/media/governance-evidence/x/y.pdf")]
    [InlineData("/MEDIA/Governance-Evidence/x/y.pdf")]
    [InlineData("/media/governance-evidence")]
    public async Task Requests_for_the_evidence_folder_get_404_for_everyone(string path)
    {
        var reached = false;
        var middleware = new WithheldMediaFileMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        await middleware.InvokeAsync(context, null!, null!, Options.Create(new MediaGovernanceOptions()));
        Assert.Equal(404, context.Response.StatusCode);
        Assert.False(reached);
    }

    [Fact]
    public async Task The_blocked_folder_follows_the_configured_name()
    {
        var middleware = new WithheldMediaFileMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/media/my-forms/x/y.pdf";
        await middleware.InvokeAsync(context, null!, null!, Options.Create(new MediaGovernanceOptions { EvidenceFolder = "my-forms" }));
        Assert.Equal(404, context.Response.StatusCode);
    }

    // ---------------------------------------------------------------- download everything

    [Fact]
    public void The_export_holds_everything_stored_about_the_person_whatever_the_options()
    {
        var p = new PersonRow
        {
            Id = Guid.NewGuid(), Name = "Tom Reyes", Email = "tom@example.com", Phone = "0123", JobTitle = "Chef", CreatedUtc = new DateTime(2026, 1, 2),
            ConsentGiven = true, ConsentUpdatedUtc = new DateTime(2026, 3, 4), ConsentGivenOn = new DateTime(2026, 3, 1), ConsentGivenBy = "Self",
            ConsentExpiresOn = new DateTime(2027, 3, 1), ConsentPermittedUses = "Web,Print",
        };
        var data = PersonDataExporter.Describe(p,
            [new TaggedImageExport("Lecture theatre", "Campus")],
            [new ActivityExport(new DateTime(2026, 3, 4), "consent.given", "Jane Editor")],
            "evidence.pdf", new DateTime(2026, 11, 17));
        Assert.Equal("Tom Reyes", data.Person.Name);
        Assert.Equal("tom@example.com", data.Person.Email);
        Assert.True(data.Consent.Given);
        Assert.Equal(new DateOnly(2026, 3, 1), data.Consent.GivenOn);
        Assert.Equal("Self", data.Consent.GivenBy);
        Assert.Equal(new DateOnly(2027, 3, 1), data.Consent.ExpiresOn);
        Assert.Equal(["Web", "Print"], data.Consent.PermittedUses);
        Assert.Equal("evidence.pdf", data.Consent.EvidenceFile);
        Assert.Equal("Lecture theatre", Assert.Single(data.TaggedImages).Name);
        Assert.Equal("Jane Editor", Assert.Single(data.Activity).By);
    }

    [Fact]
    public void The_zip_has_the_summary_and_the_evidence_file()
    {
        var data = PersonDataExporter.Describe(new PersonRow { Id = Guid.NewGuid(), Name = "Tom Reyes" }, [], [], "evidence.pdf", DateTime.UtcNow);
        var bytes = PersonDataExporter.Zip(data, "evidence.pdf", Samples.Pdf());
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal([PersonDataExporter.JsonName, "evidence.pdf"], zip.Entries.Select(e => e.FullName).ToArray());
        using var json = new StreamReader(zip.GetEntry(PersonDataExporter.JsonName)!.Open(), Encoding.UTF8);
        var text = json.ReadToEnd();
        Assert.Contains("\"name\": \"Tom Reyes\"", text);
        Assert.Contains("\"evidenceFile\": \"evidence.pdf\"", text);
    }

    [Fact]
    public void The_zip_is_just_the_summary_when_there_is_no_evidence()
    {
        var data = PersonDataExporter.Describe(new PersonRow { Id = Guid.NewGuid(), Name = "Tom Reyes" }, [], [], null, DateTime.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(PersonDataExporter.Zip(data, null, null)));
        Assert.Equal(PersonDataExporter.JsonName, Assert.Single(zip.Entries).FullName);
    }
}
