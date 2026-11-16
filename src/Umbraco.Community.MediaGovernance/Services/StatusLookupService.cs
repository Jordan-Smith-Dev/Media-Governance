using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Status for a specific set of images (cheaper than evaluating the whole library, e.g. for tree pages).</summary>
public class StatusLookupService(IImageRepository images, ISettingsService settings, TimeProvider time, IMediaUploadDates? uploads = null)
{
    /// <summary>Every problem for each image (empty list when compliant), most severe first.</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<GovernanceStatus>>> GetIssuesAsync(IReadOnlyCollection<Guid> mediaKeys)
    {
        if (mediaKeys.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<GovernanceStatus>>();

        var options = (await settings.GetAsync()).ToCore();
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var facts = await images.GetFactsAsync(mediaKeys).WithUploadDatesAsync(uploads);
        return facts.ToDictionary(f => f.MediaKey, f => StatusCalculator.Issues(f, options, today));
    }

    public async Task<IReadOnlyDictionary<Guid, GovernanceStatus>> GetStatusesAsync(IReadOnlyCollection<Guid> mediaKeys)
    {
        if (mediaKeys.Count == 0)
            return new Dictionary<Guid, GovernanceStatus>();

        var options = (await settings.GetAsync()).ToCore();
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var facts = await images.GetFactsAsync(mediaKeys).WithUploadDatesAsync(uploads);
        return facts.ToDictionary(f => f.MediaKey, f => StatusCalculator.Calculate(f, options, today));
    }
}
