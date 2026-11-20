using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeAuditRepository : IAuditRepository
{
	public List<AuditRow> Rows { get; } = new();
	public Task WriteAsync(AuditRow row) { Rows.Add(row); return Task.CompletedTask; }
	public Task<IReadOnlyList<AuditRow>> ListAsync(int take) => Task.FromResult<IReadOnlyList<AuditRow>>(Rows);

	public Task<IReadOnlyList<AuditRow>> ListForPersonAsync(Guid personId, IReadOnlyCollection<string>? actions, int take) =>
		Task.FromResult<IReadOnlyList<AuditRow>>(Rows
			.Where(r => r.PersonId == personId && (actions is null || actions.Count == 0 || actions.Contains(r.Action)))
			.Reverse().Take(take).ToList());

}
