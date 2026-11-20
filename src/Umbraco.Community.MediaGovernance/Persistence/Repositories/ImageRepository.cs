using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.People;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal class ImageRepository : IImageRepository
{
	private const int MaxKeysForTargetedQuery = 200;

	private readonly IScopeProvider _scopeProvider;
	private readonly IOptions<MediaGovernanceOptions> _options;

	public ImageRepository(IScopeProvider scopeProvider, IOptions<MediaGovernanceOptions> options)
	{
		_scopeProvider = scopeProvider;
		_options = options;
	}

	public async Task<ImageRow?> GetAsync(Guid mediaKey)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.SingleOrDefault<ImageRow>("WHERE MediaKey = @0", mediaKey);
	}

	public async Task<IReadOnlyList<Guid>> GetTaggedPersonIdsAsync(Guid mediaKey)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.Fetch<Guid>(
			$"SELECT PersonId FROM {TableNames.ImagePerson} WHERE MediaKey = @0", mediaKey);
	}

	public async Task SaveAsync(ImageRow image, IReadOnlyList<Guid> taggedPersonIds)
	{
		using var scope = _scopeProvider.CreateScope();
		if (scope.Database.Exists<ImageRow>(image.MediaKey))
			scope.Database.Update(image);
		else
			scope.Database.Insert(image);

		scope.Database.Execute($"DELETE FROM {TableNames.ImagePerson} WHERE MediaKey = @0", image.MediaKey);
		foreach (var personId in taggedPersonIds.Distinct())
			scope.Database.Insert(new ImagePersonRow { MediaKey = image.MediaKey, PersonId = personId });
		scope.Complete();
	}

	public async Task<IReadOnlyList<Guid>> ListImageKeysForPersonAsync(Guid personId)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.Fetch<Guid>($"SELECT DISTINCT MediaKey FROM {TableNames.ImagePerson} WHERE PersonId = @0", personId);
	}

	public async Task DeleteForMediaAsync(Guid mediaKey)
	{
		using var scope = _scopeProvider.CreateScope();
		scope.Database.Execute($"DELETE FROM {TableNames.ImagePerson} WHERE MediaKey = @0", mediaKey);
		scope.Database.Execute($"DELETE FROM {TableNames.Image} WHERE MediaKey = @0", mediaKey);
		scope.Database.Execute($"DELETE FROM {TableNames.Notified} WHERE MediaKey = @0", mediaKey);
		scope.Database.Execute($"DELETE FROM {TableNames.MediaFile} WHERE MediaKey = @0", mediaKey);
		scope.Complete();
	}

	public async Task<IReadOnlyDictionary<Guid, int>> CountTagsByPersonAsync()
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var tags = scope.Database.Fetch<ImagePersonRow>($"SELECT * FROM {TableNames.ImagePerson}");
		return tags.GroupBy(t => t.PersonId).ToDictionary(g => g.Key, g => g.Select(t => t.MediaKey).Distinct().Count());
	}

	public async Task<IReadOnlyList<Guid>> ListGovernedKeysAsync()
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.Fetch<Guid>($"SELECT MediaKey FROM {TableNames.Image}");
	}

	public async Task<IReadOnlyList<ImageGovernanceFacts>> GetFactsAsync(IReadOnlyCollection<Guid> mediaKeys)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var wanted = mediaKeys.Distinct().ToList();

		List<ImageRow> imageRows;
		List<ImagePersonRow> tagRows;
		List<PersonRow> personRows;
		if (wanted.Count <= MaxKeysForTargetedQuery)
		{
			// A handful of images (a page on the public site): query just those keys.
			imageRows = scope.Database.Fetch<ImageRow>($"SELECT * FROM {TableNames.Image} WHERE MediaKey IN (@keys)", new { keys = wanted });
			tagRows = scope.Database.Fetch<ImagePersonRow>($"SELECT * FROM {TableNames.ImagePerson} WHERE MediaKey IN (@keys)", new { keys = wanted });
			var taggedIds = tagRows.Select(t => t.PersonId).Distinct().ToList();
			personRows = taggedIds.Count == 0
				? []
				: scope.Database.Fetch<PersonRow>($"SELECT * FROM {TableNames.Person} WHERE Id IN (@ids)", new { ids = taggedIds });
		}
		else
		{
			// Many images (the backoffice dashboard): load the governance tables whole and join in memory,
			// which avoids provider limits on the number of query parameters.
			imageRows = scope.Database.Fetch<ImageRow>($"SELECT * FROM {TableNames.Image}");
			tagRows = scope.Database.Fetch<ImagePersonRow>($"SELECT * FROM {TableNames.ImagePerson}");
			personRows = scope.Database.Fetch<PersonRow>($"SELECT * FROM {TableNames.Person}");
		}

		var images = imageRows.ToDictionary(i => i.MediaKey);
		var tags = tagRows.ToLookup(t => t.MediaKey, t => t.PersonId);
		var o = _options.Value;
		var uses = o.PermittedUses.Any(u => !string.IsNullOrWhiteSpace(u));
		var consentByPerson = personRows
			.Select(p => ConsentFactsFactory.Create(p, o.ConsentDetails, o.ConsentExpiry, uses))
			.Where(c => c is not null).ToDictionary(c => c!.PersonId, c => c!);

		return wanted.Select(key =>
		{
			images.TryGetValue(key, out var row);
			return new ImageGovernanceFacts(
				key,
				Dates.FromDb(row?.DateTaken),
				Dates.FromDb(row?.DateObtained),
				Dates.FromDb(row?.CustomExpiry),
				row?.NoPeople ?? false,
				tags[key].ToList(),
				// Consent belongs to the person: their yes, plus any detail an option has switched on.
				tags[key].Where(consentByPerson.ContainsKey).Select(id => consentByPerson[id]).ToList());
		}).ToList();
	}
}
