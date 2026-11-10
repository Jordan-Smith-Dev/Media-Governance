using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>When each media item was uploaded (created). Used when an image has no date taken or obtained.</summary>
public interface IMediaUploadDates
{
    Task<IReadOnlyDictionary<Guid, DateOnly>> GetAsync(IReadOnlyCollection<Guid> mediaKeys);
}

public class EntityMediaUploadDates(IEntityService entities) : IMediaUploadDates
{
    private const int ChunkSize = 500;

    public Task<IReadOnlyDictionary<Guid, DateOnly>> GetAsync(IReadOnlyCollection<Guid> mediaKeys)
    {
        var result = new Dictionary<Guid, DateOnly>();
        foreach (var chunk in mediaKeys.Distinct().Chunk(ChunkSize))
        {
            foreach (var entity in entities.GetAll(UmbracoObjectTypes.Media, chunk))
                result[entity.Key] = DateOnly.FromDateTime(entity.CreateDate);
        }
        return Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(result);
    }
}

public static class UploadDateExtensions
{
    /// <summary>Fills in each image's upload date (when a source is available) so retention can start from it.</summary>
    public static async Task<IReadOnlyList<ImageGovernanceFacts>> WithUploadDatesAsync(
        this Task<IReadOnlyList<ImageGovernanceFacts>> factsTask, IMediaUploadDates? uploads)
    {
        var facts = await factsTask;
        if (uploads is null || facts.Count == 0) return facts;

        var dates = await uploads.GetAsync(facts.Select(f => f.MediaKey).ToList());
        return facts.Select(f => dates.TryGetValue(f.MediaKey, out var d) ? f with { DateUploaded = d } : f).ToList();
    }
}
