using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal class PersonRepository : IPersonRepository
{
	private readonly IScopeProvider _scopeProvider;

	public PersonRepository(IScopeProvider scopeProvider)
	{
		_scopeProvider = scopeProvider;
	}

	public async Task<PersonRow?> GetAsync(Guid id)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.SingleOrDefault<PersonRow>($"WHERE Id = @0", id);
	}

	public async Task<IReadOnlyList<PersonRow>> ListAsync(string? query)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var rows = string.IsNullOrWhiteSpace(query)
			? scope.Database.Fetch<PersonRow>($"SELECT * FROM {TableNames.Person} ORDER BY Name")
			: scope.Database.Fetch<PersonRow>(
				$"SELECT * FROM {TableNames.Person} WHERE Name LIKE @0 OR Email LIKE @0 ORDER BY Name", $"%{query.Trim()}%");
		return rows;
	}

	public async Task SaveAsync(PersonRow person)
	{
		using var scope = _scopeProvider.CreateScope();
		if (scope.Database.Exists<PersonRow>(person.Id))
			scope.Database.Update(person);
		else
			scope.Database.Insert(person);
		scope.Complete();
	}

	public async Task<IReadOnlyList<PersonRow>> GetManyAsync(IReadOnlyCollection<Guid> ids)
	{
		if (ids.Count == 0)
			return [];
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		// Filtered in memory: people are few, and this avoids provider limits on parameter counts.
		var wanted = ids.ToHashSet();
		return scope.Database.Fetch<PersonRow>($"SELECT * FROM {TableNames.Person}").Where(p => wanted.Contains(p.Id)).ToList();
	}

	public async Task EraseAsync(Guid personId)
	{
		using var scope = _scopeProvider.CreateScope();
		scope.Database.Execute($"DELETE FROM {TableNames.ImagePerson} WHERE PersonId = @0", personId);
		scope.Database.Execute($"DELETE FROM {TableNames.Person} WHERE Id = @0", personId);
		scope.Complete();
	}
}
