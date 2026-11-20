using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

public interface IPersonRepository
{
	Task<PersonRow?> GetAsync(Guid id);
	Task<IReadOnlyList<PersonRow>> ListAsync(string? query);
	Task SaveAsync(PersonRow person);

	Task<IReadOnlyList<PersonRow>> GetManyAsync(IReadOnlyCollection<Guid> ids);

	/// <summary>Deletes the person and their image tags.</summary>
	Task EraseAsync(Guid personId);
}
