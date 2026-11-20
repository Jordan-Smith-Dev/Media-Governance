using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Settings;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.Overview;

public class OverviewService
{
	private readonly IImageCatalog _catalog;
	private readonly IImageRepository _images;
	private readonly IPersonRepository _people;
	private readonly ISettingsService _settings;
	private readonly TimeProvider _time;
	private readonly IMediaUploadDates? _uploads;

	public OverviewService(
		IImageCatalog catalog,
		IImageRepository images,
		IPersonRepository people,
		ISettingsService settings,
		TimeProvider time,
		IMediaUploadDates? uploads = null)
	{
		_catalog = catalog;
		_images = images;
		_people = people;
		_settings = settings;
		_time = time;
		_uploads = uploads;
	}

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

		var ordered = Order(filtered, query.Sort, query.Descending);
		var page = ordered.Skip(Math.Max(0, query.Skip)).Take(Math.Max(0, query.Take))
			.Select(e => ToItem(e, names, today)).ToList();

		return new OverviewPage(counts, all.Count, page, ordered.Count);
	}

	// Most urgent first when ascending.
	private static int Severity(GovernanceStatus status) => status switch
	{
		GovernanceStatus.Expired => 0,
		GovernanceStatus.NoConsent => 1,
		GovernanceStatus.NeedsTagging => 2,
		GovernanceStatus.ExpiringSoon => 3,
		_ => 4,
	};

	private static List<Evaluated> Order(IEnumerable<Evaluated> items, OverviewSort sort, bool descending)
	{
		var text = StringComparer.OrdinalIgnoreCase;

		// Images without a date always come last, in either direction.
		IOrderedEnumerable<Evaluated> ByDate(Func<Evaluated, DateOnly?> key) =>
			descending
				? items.OrderBy(e => key(e) is null ? 1 : 0).ThenByDescending(e => key(e))
				: items.OrderBy(e => key(e) is null ? 1 : 0).ThenBy(e => key(e));

		var ordered = sort switch
		{
			OverviewSort.Folder => descending ? items.OrderByDescending(e => e.Info.Folder, text) : items.OrderBy(e => e.Info.Folder, text),
			OverviewSort.DateTaken => ByDate(e => e.Facts.DateTaken),
			OverviewSort.Expiry => ByDate(e => e.Expiry),
			OverviewSort.Status => descending ? items.OrderByDescending(e => Severity(e.Status)) : items.OrderBy(e => Severity(e.Status)),
			_ => descending ? items.OrderByDescending(e => e.Info.Name, text) : items.OrderBy(e => e.Info.Name, text),
		};
		return ordered.ThenBy(e => e.Info.Name, text).ToList();
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
		var options = (await _settings.GetAsync()).ToCore();
		var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
		var infos = await _catalog.ListImagesAsync();
		var facts = (await _images.GetFactsAsync(infos.Select(i => i.Key).ToList()).WithUploadDatesAsync(_uploads)).ToDictionary(f => f.MediaKey);
		var names = (await _people.ListAsync(null)).ToDictionary(p => p.Id, p => p.Name);

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
