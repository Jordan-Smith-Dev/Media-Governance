using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal interface INotifiedRepository
{
	Task<IReadOnlyDictionary<Guid, GovernanceStatus>> GetAllAsync();
	Task UpsertAsync(Guid mediaKey, GovernanceStatus status);
}
