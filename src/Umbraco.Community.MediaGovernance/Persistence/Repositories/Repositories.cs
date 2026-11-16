using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

public class PersonRepository(IScopeProvider scopeProvider) : IPersonRepository
{
    public async Task<PersonRow?> GetAsync(Guid id)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.SingleOrDefault<PersonRow>($"WHERE Id = @0", id);
    }

    public async Task<IReadOnlyList<PersonRow>> ListAsync(string? query)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        var rows = string.IsNullOrWhiteSpace(query)
            ? scope.Database.Fetch<PersonRow>($"SELECT * FROM {TableNames.Person} ORDER BY Name")
            : scope.Database.Fetch<PersonRow>(
                $"SELECT * FROM {TableNames.Person} WHERE Name LIKE @0 OR Email LIKE @0 ORDER BY Name", $"%{query.Trim()}%");
        return rows;
    }

    public async Task SaveAsync(PersonRow person)
    {
        using var scope = scopeProvider.CreateScope();
        if (scope.Database.Exists<PersonRow>(person.Id))
            scope.Database.Update(person);
        else
            scope.Database.Insert(person);
        scope.Complete();
    }

    public async Task<IReadOnlyList<PersonRow>> GetManyAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return [];
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        // Filtered in memory: people are few, and this avoids provider limits on parameter counts.
        var wanted = ids.ToHashSet();
        return scope.Database.Fetch<PersonRow>($"SELECT * FROM {TableNames.Person}").Where(p => wanted.Contains(p.Id)).ToList();
    }

    public async Task EraseAsync(Guid personId)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Execute($"DELETE FROM {TableNames.ImagePerson} WHERE PersonId = @0", personId);
        scope.Database.Execute($"DELETE FROM {TableNames.Person} WHERE Id = @0", personId);
        scope.Complete();
    }
}

public class ImageRepository(IScopeProvider scopeProvider) : IImageRepository
{
    private const int MaxKeysForTargetedQuery = 200;

    public async Task<ImageRow?> GetAsync(Guid mediaKey)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.SingleOrDefault<ImageRow>("WHERE MediaKey = @0", mediaKey);
    }

    public async Task<IReadOnlyList<Guid>> GetTaggedPersonIdsAsync(Guid mediaKey)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<Guid>(
            $"SELECT PersonId FROM {TableNames.ImagePerson} WHERE MediaKey = @0", mediaKey);
    }

    public async Task SaveAsync(ImageRow image, IReadOnlyList<Guid> taggedPersonIds)
    {
        using var scope = scopeProvider.CreateScope();
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
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<Guid>($"SELECT DISTINCT MediaKey FROM {TableNames.ImagePerson} WHERE PersonId = @0", personId);
    }

    public async Task DeleteForMediaAsync(Guid mediaKey)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Execute($"DELETE FROM {TableNames.ImagePerson} WHERE MediaKey = @0", mediaKey);
        scope.Database.Execute($"DELETE FROM {TableNames.Image} WHERE MediaKey = @0", mediaKey);
        scope.Database.Execute($"DELETE FROM {TableNames.Notified} WHERE MediaKey = @0", mediaKey);
        scope.Database.Execute($"DELETE FROM {TableNames.MediaFile} WHERE MediaKey = @0", mediaKey);
        scope.Complete();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountTagsByPersonAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        var tags = scope.Database.Fetch<ImagePersonRow>($"SELECT * FROM {TableNames.ImagePerson}");
        return tags.GroupBy(t => t.PersonId).ToDictionary(g => g.Key, g => g.Select(t => t.MediaKey).Distinct().Count());
    }

    public async Task<IReadOnlyList<Guid>> ListGovernedKeysAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<Guid>($"SELECT MediaKey FROM {TableNames.Image}");
    }

    public async Task<IReadOnlyList<ImageGovernanceFacts>> GetFactsAsync(IReadOnlyCollection<Guid> mediaKeys)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
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
        var consentedPeople = personRows.Where(p => p.ConsentGiven).Select(p => p.Id).ToHashSet();

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
                // Consent is a yes/no on the person: a "yes" counts as in force, with no dates or limits.
                tags[key].Where(consentedPeople.Contains).Select(id => new ConsentFacts(id, ConsentFacts.Always, null, null)).ToList());
        }).ToList();
    }
}

public class MediaFileRepository(IScopeProvider scopeProvider) : IMediaFileRepository
{
    public async Task<Guid?> FindKeyAsync(string normalizedPath)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        var row = scope.Database.FirstOrDefault<MediaFileRow>($"SELECT * FROM {TableNames.MediaFile} WHERE FilePath = @0", normalizedPath);
        return row?.MediaKey;
    }

    public async Task<IReadOnlyCollection<Guid>> ListKeysAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<Guid>($"SELECT MediaKey FROM {TableNames.MediaFile}");
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListPathsAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<MediaFileRow>($"SELECT * FROM {TableNames.MediaFile}").ToDictionary(r => r.MediaKey, r => r.FilePath);
    }

    public async Task UpsertAsync(Guid mediaKey, string normalizedPath)
    {
        using var scope = scopeProvider.CreateScope();
        var row = new MediaFileRow { MediaKey = mediaKey, FilePath = normalizedPath };
        if (scope.Database.Exists<MediaFileRow>(mediaKey))
            scope.Database.Update(row);
        else
            scope.Database.Insert(row);
        scope.Complete();
    }

    public async Task DeleteAsync(Guid mediaKey)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Execute($"DELETE FROM {TableNames.MediaFile} WHERE MediaKey = @0", mediaKey);
        scope.Complete();
    }
}

public class AuditRepository(IScopeProvider scopeProvider) : IAuditRepository
{
    public async Task WriteAsync(AuditRow row)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Insert(row);
        scope.Complete();
    }

    public async Task<IReadOnlyList<AuditRow>> ListAsync(int take)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database.Fetch<AuditRow>($"SELECT * FROM {TableNames.Audit} ORDER BY Id DESC");
        return rows.Take(take).ToList();
    }

}

public class NotifiedRepository(IScopeProvider scopeProvider) : INotifiedRepository
{
    public async Task<IReadOnlyDictionary<Guid, GovernanceStatus>> GetAllAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database.Fetch<NotifiedRow>($"SELECT * FROM {TableNames.Notified}");
        return rows.ToDictionary(r => r.MediaKey, r => (GovernanceStatus)r.LastStatus);
    }

    public async Task UpsertAsync(Guid mediaKey, GovernanceStatus status)
    {
        using var scope = scopeProvider.CreateScope();
        var row = new NotifiedRow { MediaKey = mediaKey, LastStatus = (int)status, NotifiedUtc = DateTime.UtcNow };
        if (scope.Database.Exists<NotifiedRow>(mediaKey))
            scope.Database.Update(row);
        else
            scope.Database.Insert(row);
        scope.Complete();
    }
}
