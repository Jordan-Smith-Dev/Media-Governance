using Microsoft.Extensions.Logging;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Evidence;
using Umbraco.Community.MediaGovernance.Services.Settings;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.People;

public class PersonService
{
	private readonly IPersonRepository _people;
	private readonly IAuditService _audit;
	private readonly ISettingsService _settings;
	private readonly IEvidenceStore _evidence;
	private readonly TimeProvider _time;
	private readonly Microsoft.Extensions.Logging.ILogger<PersonService>? _logger;

	public PersonService(
		IPersonRepository people,
		IAuditService audit,
		ISettingsService settings,
		IEvidenceStore evidence,
		TimeProvider time,
		Microsoft.Extensions.Logging.ILogger<PersonService>? logger = null)
	{
		_people = people;
		_audit = audit;
		_settings = settings;
		_evidence = evidence;
		_time = time;
		_logger = logger;
	}

	public static readonly string[] EvidenceActions = ["consent.evidence", "consent.evidence.viewed", "consent.evidence.removed"];

	private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

	public Task<IReadOnlyList<PersonRow>> ListAsync(string? query) => _people.ListAsync(query);

	public Task<PersonRow?> GetAsync(Guid id) => _people.GetAsync(id);

	public async Task<PersonRow> CreateAsync(PersonInput input, string actor)
	{
		var row = new PersonRow { Id = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow };
		Apply(row, input);
		await _people.SaveAsync(row);
		await _audit.WriteAsync(actor, "person.created", personId: row.Id);
		return row;
	}

	public async Task<PersonRow> UpdateAsync(Guid id, PersonInput input, string actor)
	{
		var row = await _people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");
		Apply(row, input);
		await _people.SaveAsync(row);
		await _audit.WriteAsync(actor, "person.updated", personId: id);
		return row;
	}

	/// <summary>Records a plain yes or no. See the overload for the optional detail.</summary>
	public Task<PersonRow> SetConsentAsync(Guid id, bool given, string actor) => SetConsentAsync(id, new ConsentChange(given), actor);

	/// <summary>
	/// Records whether the person has given consent, with whichever optional detail is switched on. Every change is audited with
	/// who made it. Detail saved while an option was on is kept if it is switched off, and simply ignored.
	/// </summary>
	public async Task<PersonRow> SetConsentAsync(Guid id, ConsentChange change, string actor)
	{
		var options = await _settings.GetAsync();
		var row = await _people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");
		var wasGiven = row.ConsentGiven;
		var detailChanged = false;

		if (change.Given)
		{
			DateOnly? givenOn = null;
			if (options.ConsentDetails)
			{
				givenOn = change.GivenOn ?? (row.ConsentGivenOn is { } g ? DateOnly.FromDateTime(g) : Today);
				if (givenOn > Today)
					throw new ArgumentException("Consent cannot be given in the future.");
				var by = string.IsNullOrWhiteSpace(change.GivenBy) ? row.ConsentGivenBy ?? ConsentGivenBy.Self : change.GivenBy;
				if (!ConsentGivenBy.All.Contains(by))
					throw new ArgumentException("Consent must be given by the person, a parent or guardian, or a legal representative.");
				detailChanged |= row.ConsentGivenOn != givenOn.Value.ToDateTime(TimeOnly.MinValue) || row.ConsentGivenBy != by;
				row.ConsentGivenOn = givenOn.Value.ToDateTime(TimeOnly.MinValue);
				row.ConsentGivenBy = by;
			}

			if (options.ConsentExpiry)
			{
				var start = givenOn ?? (row.ConsentGivenOn is { } s ? DateOnly.FromDateTime(s) : (DateOnly?)null);
				if (change.ExpiresOn is { } end && start is { } from && end <= from)
					throw new ArgumentException("The expiry must be after the date consent was given.");
				var expires = change.ExpiresOn?.ToDateTime(TimeOnly.MinValue);
				detailChanged |= row.ConsentExpiresOn != expires;
				row.ConsentExpiresOn = expires;
			}

			if (options.UsesOn)
			{
				var allowed = options.PermittedUses!;
				var uses = (change.PermittedUses ?? []).Select(u => u.Trim()).Where(u => u.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				if (uses.Count == 0)
					throw new ArgumentException("Choose at least one permitted use.");
				var unknown = uses.FirstOrDefault(u => !allowed.Contains(u, StringComparer.OrdinalIgnoreCase));
				if (unknown is not null)
					throw new ArgumentException($"'{unknown}' is not one of the permitted uses.");
				// Stored with the spelling from the configured list.
				var stored = string.Join(",", uses.Select(u => allowed.First(a => a.Equals(u, StringComparison.OrdinalIgnoreCase))));
				detailChanged |= row.ConsentPermittedUses != stored;
				row.ConsentPermittedUses = stored;
			}
		}

		if (wasGiven == change.Given && !detailChanged)
			return row;

		row.ConsentGiven = change.Given;
		row.ConsentUpdatedUtc = DateTime.UtcNow;
		await _people.SaveAsync(row);
		await _audit.WriteAsync(actor, !change.Given ? "consent.withdrawn" : wasGiven ? "consent.updated" : "consent.given", personId: id);
		return row;
	}

	// ---------------------------------------------------------------- evidence (ConsentEvidence option)

	private async Task RequireEvidenceOnAsync()
	{
		if (!(await _settings.GetAsync()).ConsentEvidence)
			throw new InvalidOperationException("Consent evidence is not switched on. Set ConsentEvidence to true in appsettings.");
	}

	/// <summary>Attaches (or replaces) the evidence for a person's consent. The old file is only deleted once the new one is safely stored.</summary>
	public async Task<PersonRow> AttachEvidenceAsync(Guid id, string fileName, Stream data, string actor)
	{
		await RequireEvidenceOnAsync();
		var row = await _people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");

		var oldPath = row.ConsentEvidencePath;
		var newPath = await _evidence.SaveAsync(id, fileName, data);   // throws before anything is lost

		row.ConsentEvidencePath = newPath;
		await _people.SaveAsync(row);
		if (oldPath is not null && oldPath != newPath)
			await _evidence.DeleteAsync(oldPath);
		await _audit.WriteAsync(actor, "consent.evidence", personId: id);
		return row;
	}

	/// <summary>Opens the evidence for viewing, or returns null if none is on file. Each view is audited.</summary>
	public async Task<EvidenceFile?> OpenEvidenceAsync(Guid id, string actor)
	{
		await RequireEvidenceOnAsync();
		var row = await _people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");
		if (row.ConsentEvidencePath is null)
			return null;

		var extension = Path.GetExtension(row.ConsentEvidencePath);
		var contentType = EvidenceContent.ContentTypeFor(extension);
		if (contentType is null)
			return null;

		var stream = await _evidence.OpenReadAsync(row.ConsentEvidencePath);
		if (stream is null)
			return null;

		await _audit.WriteAsync(actor, "consent.evidence.viewed", personId: id);
		return new EvidenceFile(stream, contentType, $"consent-evidence{extension.ToLowerInvariant()}");
	}

	/// <summary>Removes the evidence file. Does nothing if none is on file.</summary>
	public async Task RemoveEvidenceAsync(Guid id, string actor)
	{
		await RequireEvidenceOnAsync();
		var row = await _people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");
		if (row.ConsentEvidencePath is null)
			return;

		var path = row.ConsentEvidencePath;
		row.ConsentEvidencePath = null;
		await _people.SaveAsync(row);   // unlink first: a leftover file is harmless, a dangling link is not
		try
		{
			await _evidence.DeleteAsync(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// The record is already unlinked; an orphaned file is removed when the person is deleted.
		}
		await _audit.WriteAsync(actor, "consent.evidence.removed", personId: id);
	}

	/// <summary>Uploads, views and removals of this person's evidence, newest first.</summary>
	public async Task<IReadOnlyList<EvidenceEvent>> EvidenceHistoryAsync(Guid id, int take = 50)
	{
		await RequireEvidenceOnAsync();
		if (await _people.GetAsync(id) is null)
			throw new KeyNotFoundException($"Person {id} not found.");
		var entries = await _audit.ListForPersonAsync(id, EvidenceActions, take);
		return entries.Select(e => new EvidenceEvent(e.Action, e.WhenUtc, e.ActorKey)).ToList();
	}

	/// <summary>Type and size of the person's evidence, or null if there is none (or the file is missing).</summary>
	public async Task<(string Type, long? SizeBytes)?> EvidenceInfoAsync(PersonRow person)
	{
		if (person.ConsentEvidencePath is null)
			return null;
		return (EvidenceContent.TypeLabel(person.ConsentEvidencePath), await _evidence.GetSizeAsync(person.ConsentEvidencePath));
	}

	/// <summary>
	/// Permanently removes the person, their image tags and any evidence files. The audit entry records only that a
	/// deletion happened. Evidence is deleted even if the option has since been switched off.
	/// </summary>
	public async Task EraseAsync(Guid id, string actor)
	{
		if (await _people.GetAsync(id) is null)
			throw new KeyNotFoundException($"Person {id} not found.");

		await _people.EraseAsync(id);
		try
		{
			await _evidence.DeleteForPersonAsync(id);
		}
		catch (Exception ex)
		{
			// The records are already gone; a locked file must not hide that or skip the audit entry.
			_logger?.LogWarning(ex, "Evidence files for deleted person {PersonId} could not be fully deleted.", id);
		}

		await _audit.WriteAsync(actor, "person.erased", personId: id, detail: "Person, image tags and evidence deleted on request.");
	}

	private static void Apply(PersonRow row, PersonInput input)
	{
		if (string.IsNullOrWhiteSpace(input.Name))
			throw new ArgumentException("A person needs a name.", nameof(input));

		row.Name = input.Name.Trim();
		row.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
		row.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
		row.JobTitle = string.IsNullOrWhiteSpace(input.JobTitle) ? null : input.JobTitle.Trim();
		row.LinkedMemberKey = input.LinkedMemberKey;
		row.LinkedUserKey = input.LinkedUserKey;
	}
}
