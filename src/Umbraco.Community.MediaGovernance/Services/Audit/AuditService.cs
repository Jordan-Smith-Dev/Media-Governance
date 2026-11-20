using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services.Audit;

internal class AuditService : IAuditService
{
	private readonly IAuditRepository _repository;

	public AuditService(IAuditRepository repository)
	{
		_repository = repository;
	}

	public Task WriteAsync(string actor, string action, Guid? mediaKey = null, Guid? personId = null, string? detail = null) =>
		_repository.WriteAsync(new AuditRow
		{
			WhenUtc = DateTime.UtcNow,
			ActorKey = actor,
			Action = action,
			MediaKey = mediaKey,
			PersonId = personId,
			Detail = detail
		});

	public Task<IReadOnlyList<AuditRow>> ListForPersonAsync(Guid personId, IReadOnlyCollection<string>? actions, int take) =>
		_repository.ListForPersonAsync(personId, actions, take);
}
