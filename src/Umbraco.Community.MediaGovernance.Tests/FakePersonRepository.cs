using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakePersonRepository : IPersonRepository
{
	public Dictionary<Guid, PersonRow> People { get; } = new();
	public List<Guid> Erased { get; } = new();

	public Task<PersonRow?> GetAsync(Guid id) => Task.FromResult(People.GetValueOrDefault(id));
	public Task<IReadOnlyList<PersonRow>> ListAsync(string? query) =>
		Task.FromResult<IReadOnlyList<PersonRow>>(People.Values.ToList());
	public Task SaveAsync(PersonRow person) { People[person.Id] = person; return Task.CompletedTask; }
	public Task<IReadOnlyList<PersonRow>> GetManyAsync(IReadOnlyCollection<Guid> ids) =>
		Task.FromResult<IReadOnlyList<PersonRow>>(People.Values.Where(p => ids.Contains(p.Id)).ToList());
	public Task EraseAsync(Guid personId)
	{
		Erased.Add(personId);
		People.Remove(personId);
		return Task.CompletedTask;
	}
}
