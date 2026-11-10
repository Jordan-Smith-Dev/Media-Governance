using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>What is on file for a consent, for display: the type and size of the evidence.</summary>
public sealed record EvidenceInfo(string Type, long? SizeBytes);

/// <summary>One thing that happened to a consent's evidence. The actor is a raw key; resolve it to a name for display.</summary>
public sealed record EvidenceEvent(string Action, DateTime WhenUtc, string ActorKey);

/// <summary>An evidence file ready to stream. The caller disposes <see cref="Content"/>.</summary>
public sealed record EvidenceFile(Stream Content, string ContentType, string FileName);

public class ConsentService(
    IConsentRepository consents,
    IImageRepository images,
    IEvidenceStore evidence,
    IAuditService audit,
    ISettingsService settings)
{
    public Task<IReadOnlyList<ConsentRow>> ListForImageAsync(Guid mediaKey) => consents.ListForImageAsync(mediaKey);

    public async Task<ConsentRow> AddAsync(Guid mediaKey, ConsentInput input, string actor)
    {
        if (!ConsentGivenBy.All.Contains(input.GivenBy))
            throw new ArgumentException("Consent must be given by the person, a parent or guardian, or a legal representative.", nameof(input));
        if (input.PermittedUses.Count == 0)
            throw new ArgumentException("Choose at least one permitted use.", nameof(input));
        var allowedUses = (await settings.GetAsync()).PermittedUses;
        var unknown = input.PermittedUses.FirstOrDefault(u => !allowedUses.Contains(u));
        if (unknown is not null)
            throw new ArgumentException($"'{unknown}' is not one of the permitted uses set up in Settings.", nameof(input));

        var tagged = await images.GetTaggedPersonIdsAsync(mediaKey);
        if (!tagged.Contains(input.PersonId))
            throw new InvalidOperationException("Consent can only be recorded for a person tagged on the image.");

        var row = new ConsentRow
        {
            Id = Guid.NewGuid(),
            MediaKey = mediaKey,
            PersonId = input.PersonId,
            GivenOn = Dates.ToDb(input.GivenOn),
            GivenBy = input.GivenBy,
            PermittedUses = string.Join(",", input.PermittedUses),
            ExpiresOn = Dates.ToDb(input.ExpiresOn),
            Notes = input.Notes
        };
        await consents.SaveAsync(row);
        await audit.WriteAsync(actor, "consent.added", mediaKey, input.PersonId);
        return row;
    }

    public async Task WithdrawAsync(Guid consentId, DateOnly on, string actor)
    {
        var row = await consents.GetAsync(consentId) ?? throw new KeyNotFoundException($"Consent {consentId} not found.");
        row.WithdrawnOn = Dates.ToDb(on);
        await consents.SaveAsync(row);
        await audit.WriteAsync(actor, "consent.withdrawn", row.MediaKey, row.PersonId);
    }

    public async Task AttachEvidenceAsync(Guid consentId, string fileName, Stream data, string actor)
    {
        var row = await consents.GetAsync(consentId) ?? throw new KeyNotFoundException($"Consent {consentId} not found.");
        var oldPath = row.EvidencePath;
        var newPath = await evidence.SaveAsync(row.PersonId, row.Id, fileName, data);   // throws before anything is lost

        row.EvidencePath = newPath;
        await consents.SaveAsync(row);
        if (oldPath is not null && oldPath != newPath)
            await evidence.DeleteAsync([oldPath]);
        await audit.WriteAsync(actor, "consent.evidence", row.MediaKey, row.PersonId);
    }

    /// <summary>Type and size of the evidence for each consent that has some, keyed by consent id.</summary>
    public async Task<IReadOnlyDictionary<Guid, EvidenceInfo>> EvidenceInfoAsync(IEnumerable<ConsentRow> rows)
    {
        var result = new Dictionary<Guid, EvidenceInfo>();
        foreach (var row in rows.Where(r => r.EvidencePath is not null))
        {
            var type = Path.GetExtension(row.EvidencePath!).ToLowerInvariant() switch
            {
                ".pdf" => "PDF",
                ".png" => "PNG",
                ".jpg" or ".jpeg" => "JPG",
                _ => "File",
            };
            result[row.Id] = new EvidenceInfo(type, await evidence.GetSizeAsync(row.EvidencePath!));
        }
        return result;
    }

    private static readonly string[] EvidenceActions = ["consent.evidence", "consent.evidence.viewed", "consent.evidence.removed"];

    /// <summary>Uploads, views and removals of evidence for this consent's person on this image, newest first.</summary>
    public async Task<IReadOnlyList<EvidenceEvent>> EvidenceHistoryAsync(Guid consentId, int take = 50)
    {
        var row = await consents.GetAsync(consentId) ?? throw new KeyNotFoundException($"Consent {consentId} not found.");
        var entries = await audit.ListForAsync(row.MediaKey, row.PersonId, EvidenceActions, take);
        return entries.Select(e => new EvidenceEvent(e.Action, e.WhenUtc, e.ActorKey)).ToList();
    }

    /// <summary>Removes a consent's evidence file. Does nothing if none is on file.</summary>
    public async Task RemoveEvidenceAsync(Guid consentId, string actor)
    {
        var row = await consents.GetAsync(consentId) ?? throw new KeyNotFoundException($"Consent {consentId} not found.");
        if (row.EvidencePath is null) return;

        var path = row.EvidencePath;
        row.EvidencePath = null;
        await consents.SaveAsync(row);   // unlink first: a leftover file is harmless, a dangling link is not
        try
        {
            await evidence.DeleteAsync([path]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The record is already unlinked; an orphaned file is removed when the person is erased.
        }
        await audit.WriteAsync(actor, "consent.evidence.removed", row.MediaKey, row.PersonId);
    }

    /// <summary>Opens a consent's evidence for viewing, or returns null if none is on file. Each view is audited.</summary>
    public async Task<EvidenceFile?> OpenEvidenceAsync(Guid consentId, string actor)
    {
        var row = await consents.GetAsync(consentId) ?? throw new KeyNotFoundException($"Consent {consentId} not found.");
        if (row.EvidencePath is null) return null;

        var extension = Path.GetExtension(row.EvidencePath);
        var contentType = EvidenceContent.ContentTypeFor(extension);
        if (contentType is null) return null;

        var stream = await evidence.OpenReadAsync(row.EvidencePath);
        if (stream is null) return null;

        await audit.WriteAsync(actor, "consent.evidence.viewed", row.MediaKey, row.PersonId);
        return new EvidenceFile(stream, contentType, $"consent-evidence-{row.Id}{extension.ToLowerInvariant()}");
    }
}
