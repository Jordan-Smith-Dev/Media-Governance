using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

public class ImageGovernanceService(IImageRepository images, IPersonRepository people, IAuditService audit)
{
    public async Task<(ImageRow Image, IReadOnlyList<Guid> TaggedPersonIds)> GetAsync(Guid mediaKey)
    {
        var row = await images.GetAsync(mediaKey) ?? new ImageRow { MediaKey = mediaKey };
        return (row, await images.GetTaggedPersonIdsAsync(mediaKey));
    }

    public async Task SaveAsync(Guid mediaKey, ImageGovernanceUpdate update, string actor)
    {
        foreach (var personId in update.TaggedPersonIds.Distinct())
        {
            if (await people.GetAsync(personId) is null)
                throw new ArgumentException($"Person {personId} does not exist (they may have been erased).", nameof(update));
        }

        var row = new ImageRow
        {
            MediaKey = mediaKey,
            DateTaken = Dates.ToDb(update.DateTaken),
            DateObtained = Dates.ToDb(update.DateObtained),
            CustomExpiry = Dates.ToDb(update.CustomExpiry),
            NoPeople = update.NoPeople
        };
        await images.SaveAsync(row, update.TaggedPersonIds);
        await audit.WriteAsync(actor, "image.updated", mediaKey);
    }

    public const int MaxBulk = 500;

    /// <summary>
    /// Applies one change to many images, keeping each image's other details and tags as they are.
    /// "No people" is skipped for any image that already has someone tagged.
    /// </summary>
    public async Task<BulkImageResult> BulkAsync(IEnumerable<Guid> mediaKeys, BulkImageChange change, string actor)
    {
        var chosen = new[] { change.NoPeople, change.CustomExpiry is not null, change.ClearCustomExpiry }.Count(c => c);
        if (chosen != 1)
            throw new ArgumentException("Choose exactly one change: mark as no people, set an expiry date, or clear the expiry date.", nameof(change));

        var keys = mediaKeys.Distinct().ToList();
        if (keys.Count == 0) throw new ArgumentException("Choose at least one image.", nameof(mediaKeys));
        if (keys.Count > MaxBulk) throw new ArgumentException($"Choose {MaxBulk} images or fewer at a time.", nameof(mediaKeys));

        var updated = new List<Guid>();
        var skipped = new List<Guid>();
        foreach (var key in keys)
        {
            var row = await images.GetAsync(key) ?? new ImageRow { MediaKey = key };
            var tagged = await images.GetTaggedPersonIdsAsync(key);

            if (change.NoPeople)
            {
                if (tagged.Count > 0) { skipped.Add(key); continue; }   // someone is tagged, so "no people" would be wrong
                row.NoPeople = true;
            }
            else
            {
                row.CustomExpiry = change.ClearCustomExpiry ? null : Dates.ToDb(change.CustomExpiry);
            }

            await images.SaveAsync(row, tagged);
            await audit.WriteAsync(actor, "image.updated", key);
            updated.Add(key);
        }
        return new BulkImageResult(updated, skipped);
    }
}
