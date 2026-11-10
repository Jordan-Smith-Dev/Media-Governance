using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Lists the image media items the dashboard governs.</summary>
public interface IImageCatalog
{
    Task<IReadOnlyList<ImageInfo>> ListImagesAsync();
}

public class OverviewService(
    IImageCatalog catalog,
    IImageRepository images,
    IPersonRepository people,
    ISettingsService settings,
    TimeProvider time,
    IMediaUploadDates? uploads = null)
{
    private record Evaluated(ImageInfo Info, ImageGovernanceFacts Facts, GovernanceStatus Status, DateOnly? Expiry, ExpirySource Source, IReadOnlyList<GovernanceStatus> Issues);

    public async Task<OverviewPage> QueryAsync(OverviewQuery query)
    {
        var (all, names, today) = await EvaluateAsync();

        var counts = Enum.GetValues<GovernanceStatus>().ToDictionary(s => s, s => all.Count(e => e.Status == s));

        IEnumerable<Evaluated> filtered = all;
        if (query.Status is { } status)
            filtered = filtered.Where(e => e.Status == status);
        if (query.Keys is { } keys)
        {
            var wanted = keys.ToHashSet();
            filtered = filtered.Where(e => wanted.Contains(e.Info.Key));
        }
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var f = query.Filter.Trim();
            filtered = filtered.Where(e =>
                Contains(e.Info.Name, f) || Contains(e.Info.Folder, f) ||
                e.Facts.TaggedPersonIds.Any(p => names.TryGetValue(p, out var n) && Contains(n, f)));
        }

        var ordered = filtered.OrderBy(e => e.Info.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var page = ordered.Skip(Math.Max(0, query.Skip)).Take(Math.Max(0, query.Take))
            .Select(e => ToItem(e, names, today)).ToList();

        return new OverviewPage(counts, all.Count, page, ordered.Count);
    }

    /// <summary>Status per image, for the media tree indicators.</summary>
    public async Task<IReadOnlyDictionary<Guid, GovernanceStatus>> StatusMapAsync()
    {
        var (all, _, _) = await EvaluateAsync();
        return all.ToDictionary(e => e.Info.Key, e => e.Status);
    }

    /// <summary>All items without paging, for export.</summary>
    public async Task<IReadOnlyList<OverviewItem>> AllItemsAsync(OverviewQuery query) =>
        (await QueryAsync(query with { Skip = 0, Take = int.MaxValue })).Items;

    private async Task<(List<Evaluated> All, Dictionary<Guid, string> Names, DateOnly Today)> EvaluateAsync()
    {
        var options = (await settings.GetAsync()).ToCore();
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var infos = await catalog.ListImagesAsync();
        var facts = (await images.GetFactsAsync(infos.Select(i => i.Key).ToList()).WithUploadDatesAsync(uploads)).ToDictionary(f => f.MediaKey);
        var names = (await people.ListAsync(null)).ToDictionary(p => p.Id, p => p.Name);

        var all = infos.Select(info =>
        {
            var f = facts[info.Key];
            var issues = StatusCalculator.Issues(f, options, today);
            return new Evaluated(info, f, issues.Count == 0 ? GovernanceStatus.Compliant : issues[0], StatusCalculator.EffectiveExpiry(f, options), StatusCalculator.ExpirySourceOf(f, options), issues);
        }).ToList();
        return (all, names, today);
    }

    private static OverviewItem ToItem(Evaluated e, Dictionary<Guid, string> names, DateOnly today)
    {
        var tagged = e.Facts.TaggedPersonIds;
        var consent = tagged.Count == 0
            ? "—"
            : tagged.All(p => StatusCalculator.HasActiveConsent(e.Facts, p, today)) ? "Given" : "Not given";

        return new OverviewItem(
            e.Info.Key, e.Info.Name, e.Info.Folder,
            tagged.Select(p => names.GetValueOrDefault(p, "Unknown")).ToList(),
            e.Facts.DateTaken, e.Expiry,
            e.Expiry is { } x ? x.DayNumber - today.DayNumber : null,
            consent, e.Status, e.Source, e.Issues, e.Facts.NoPeople);
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
