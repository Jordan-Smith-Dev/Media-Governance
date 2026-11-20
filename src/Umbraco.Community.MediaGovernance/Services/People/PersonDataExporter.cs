using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Evidence;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Extensions;
using IUserService = Umbraco.Cms.Core.Services.IUserService;

namespace Umbraco.Community.MediaGovernance.Services.People;

/// <summary>Builds the downloadable "everything we hold about this person" file: a JSON summary, plus the evidence file if there is one.</summary>
public class PersonDataExporter
{
	private readonly IPersonRepository _people;
	private readonly IImageRepository _images;
	private readonly IImageCatalog _catalog;
	private readonly IAuditService _audit;
	private readonly IEvidenceStore _evidence;
	private readonly IUserService _users;

	public PersonDataExporter(
		IPersonRepository people,
		IImageRepository images,
		IImageCatalog catalog,
		IAuditService audit,
		IEvidenceStore evidence,
		IUserService users)
	{
		_people = people;
		_images = images;
		_catalog = catalog;
		_audit = audit;
		_evidence = evidence;
		_users = users;
	}

	public const string JsonName = "person-data.json";

	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	/// <summary>The zip for one person, or null if they do not exist. Everything stored is included, whatever options are switched on.</summary>
	public async Task<(byte[] Zip, string FileName)?> BuildAsync(Guid personId, string actor)
	{
		var person = await _people.GetAsync(personId);
		if (person is null)
			return null;

		var imageKeys = await _images.ListImageKeysForPersonAsync(personId);
		var info = (await _catalog.ListImagesAsync()).ToDictionary(i => i.Key);
		var tagged = imageKeys.Where(info.ContainsKey).Select(k => new TaggedImageExport(info[k].Name, info[k].Folder))
			.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

		var entries = await _audit.ListForPersonAsync(personId, null, 1000);
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
			await using var stream = await _evidence.OpenReadAsync(path);
			if (stream is not null)
			{
				using var copy = new MemoryStream();
				await stream.CopyToAsync(copy);
				evidenceBytes = copy.ToArray();
				evidenceName = "evidence" + Path.GetExtension(path).ToLowerInvariant();
			}
		}

		var data = Describe(person, tagged, activity, evidenceName, DateTime.UtcNow);
		await _audit.WriteAsync(actor, "person.exported", personId: personId);
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

	public static byte[] Zip(PersonDataExport data, string? evidenceName, byte[]? _evidence)
	{
		using var output = new MemoryStream();
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
		{
			Add(zip, JsonName, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, Json)));
			if (evidenceName is not null && _evidence is not null)
				Add(zip, evidenceName, _evidence);
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
		if (key == Actors.System)
			return "System";
		if (!Guid.TryParse(key, out var guid))
			return key;
		try
		{
			return (await _users.GetAsync(guid))?.Name ?? key;
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
