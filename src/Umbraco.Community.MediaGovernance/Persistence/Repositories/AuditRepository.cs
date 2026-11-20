using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal class AuditRepository : IAuditRepository
{
	private readonly IScopeProvider _scopeProvider;

	public AuditRepository(IScopeProvider scopeProvider)
	{
		_scopeProvider = scopeProvider;
	}

	public async Task WriteAsync(AuditRow row)
	{
		using var scope = _scopeProvider.CreateScope();
		scope.Database.Insert(row);
		scope.Complete();
	}

	public async Task<IReadOnlyList<AuditRow>> ListForPersonAsync(Guid personId, IReadOnlyCollection<string>? actions, int take)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var rows = scope.Database.Fetch<AuditRow>($"SELECT * FROM {TableNames.Audit} WHERE PersonId = @0 ORDER BY Id DESC", personId);
		if (actions is { Count: > 0 })
			rows = rows.Where(r => actions.Contains(r.Action)).ToList();
		return rows.Take(take).ToList();
	}

	public async Task<IReadOnlyList<AuditRow>> ListAsync(int take)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var rows = scope.Database.Fetch<AuditRow>($"SELECT * FROM {TableNames.Audit} ORDER BY Id DESC");
		return rows.Take(take).ToList();
	}

}
