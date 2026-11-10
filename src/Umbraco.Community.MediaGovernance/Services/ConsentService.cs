using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

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
}
