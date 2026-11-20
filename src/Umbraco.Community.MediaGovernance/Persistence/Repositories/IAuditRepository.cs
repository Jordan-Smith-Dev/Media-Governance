using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal interface IAuditRepository
{
	Task WriteAsync(AuditRow row);
	Task<IReadOnlyList<AuditRow>> ListAsync(int take);

	/// <summary>Newest first: entries about one person, optionally limited to the given actions.</summary>
	Task<IReadOnlyList<AuditRow>> ListForPersonAsync(Guid personId, IReadOnlyCollection<string>? actions, int take);

}
