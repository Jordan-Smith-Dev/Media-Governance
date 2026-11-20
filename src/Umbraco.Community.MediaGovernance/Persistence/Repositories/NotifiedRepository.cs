using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal class NotifiedRepository : INotifiedRepository
{
	private readonly IScopeProvider _scopeProvider;

	public NotifiedRepository(IScopeProvider scopeProvider)
	{
		_scopeProvider = scopeProvider;
	}

	public async Task<IReadOnlyDictionary<Guid, GovernanceStatus>> GetAllAsync()
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var rows = scope.Database.Fetch<NotifiedRow>($"SELECT * FROM {TableNames.Notified}");
		return rows.ToDictionary(r => r.MediaKey, r => (GovernanceStatus)r.LastStatus);
	}

	public async Task UpsertAsync(Guid mediaKey, GovernanceStatus status)
	{
		using var scope = _scopeProvider.CreateScope();
		var row = new NotifiedRow { MediaKey = mediaKey, LastStatus = (int)status, NotifiedUtc = DateTime.UtcNow };
		if (scope.Database.Exists<NotifiedRow>(mediaKey))
			scope.Database.Update(row);
		else
			scope.Database.Insert(row);
		scope.Complete();
	}
}
