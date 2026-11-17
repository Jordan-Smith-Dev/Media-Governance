using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Everything held about one person, for a subject access request (GDPR Art. 15).</summary>
public record PersonDataExport(
    DateTime ExportedUtc,
    PersonDetails Person,
    ConsentDetailsExport Consent,
    IReadOnlyList<TaggedImageExport> TaggedImages,
    IReadOnlyList<ActivityExport> Activity);

public record PersonDetails(Guid Id, string Name, string? Email, string? Phone, string? JobTitle, Guid? LinkedMemberKey, Guid? LinkedUserKey, DateTime CreatedUtc);

public record ConsentDetailsExport(bool Given, DateTime? LastChangedUtc, DateOnly? GivenOn, string? GivenBy, DateOnly? ExpiresOn, IReadOnlyList<string> PermittedUses, string? EvidenceFile);

public record TaggedImageExport(string Name, string Folder);

/// <summary>One audit entry about this person: what happened, when, and who did it (a user name where it can be found).</summary>
public record ActivityExport(DateTime WhenUtc, string Action, string By);

/// <summary>Builds the downloadable "everything we hold about this person" file: a JSON summary, plus the evidence file if there is one.</summary>
public class PersonDataExporter(
    IPersonRepository people,
    IImageRepository images,
    IImageCatalog catalog,
    IAuditService audit,
    IEvidenceStore evidence,
    IUserService users)
{
    public const string JsonName = "person-data.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The zip for one person, or null if they do not exist. Everything stored is included, whatever options are switched on.</summary>
    public async Task<(byte[] Zip, string FileName)?> BuildAsync(Guid personId, string actor)
    {
        var person = await people.GetAsync(personId);
        if (person is null) return null;

        var imageKeys = await images.ListImageKeysForPersonAsync(personId);
        var info = (await catalog.ListImagesAsync()).ToDictionary(i => i.Key);
        var tagged = imageKeys.Where(info.ContainsKey).Select(k => new TaggedImageExport(info[k].Name, info[k].Folder))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var entries = await audit.ListForPersonAsync(personId, null, 1000);
        var names = new Dictionary<string, string>();
        var activity = new List<ActivityExport>();
        foreach (var e in entries.OrderBy(e => e.WhenUtc))
        {
            if (!names.TryGetValue(e.ActorKey, out var by))
                names[e.ActorKey] = by = await ActorNameAsync(e.ActorKey);
            activity.Add(new ActivityExport(e.WhenUtc, e.Action, by));
        }

        string? evidenceName = null;
        byte[]? evidenceBytes = null;
        if (person.ConsentEvidencePath is { } path)
        {
            await using var stream = await evidence.OpenReadAsync(path);
            if (stream is not null)
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy);
                evidenceBytes = copy.ToArray();
                evidenceName = "evidence" + Path.GetExtension(path).ToLowerInvariant();
            }
        }

        var data = Describe(person, tagged, activity, evidenceName, DateTime.UtcNow);
        await audit.WriteAsync(actor, "person.exported", personId: personId);
        return (Zip(data, evidenceName, evidenceBytes), $"person-data-{Slug(person.Name)}.zip");
    }

    /// <summary>The summary of a person. Pure, so it can be tested without a database.</summary>
    public static PersonDataExport Describe(PersonRow p, IReadOnlyList<TaggedImageExport> tagged, IReadOnlyList<ActivityExport> activity, string? evidenceFile, DateTime now) =>
        new(now,
            new PersonDetails(p.Id, p.Name, p.Email, p.Phone, p.JobTitle, p.LinkedMemberKey, p.LinkedUserKey, p.CreatedUtc),
            new ConsentDetailsExport(
                p.ConsentGiven, p.ConsentUpdatedUtc,
                p.ConsentGivenOn is { } g ? DateOnly.FromDateTime(g) : null, p.ConsentGivenBy,
                p.ConsentExpiresOn is { } x ? DateOnly.FromDateTime(x) : null,
                ConsentFactsFactory.Split(p.ConsentPermittedUses), evidenceFile),
            tagged, activity);

    public static byte[] Zip(PersonDataExport data, string? evidenceName, byte[]? evidence)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, JsonName, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, Json)));
            if (evidenceName is not null && evidence is not null) Add(zip, evidenceName, evidence);
        }
        return output.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private async Task<string> ActorNameAsync(string key)
    {
        if (key == Actors.System) return "System";
        if (!Guid.TryParse(key, out var guid)) return key;
        try
        {
            return (await users.GetAsync(guid))?.Name ?? key;
        }
        catch
        {
            return key;
        }
    }

    private static string Slug(string name)
    {
        var chars = name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "person" : slug.Length > 40 ? slug[..40] : slug;
    }
}
