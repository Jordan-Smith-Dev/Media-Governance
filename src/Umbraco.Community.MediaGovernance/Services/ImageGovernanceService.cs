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
}
