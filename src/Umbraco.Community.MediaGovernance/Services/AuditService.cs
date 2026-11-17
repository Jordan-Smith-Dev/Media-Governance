using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

public interface IAuditService
{
    /// <summary>Writes an audit entry. <paramref name="detail"/> must never contain personal data.</summary>
    Task WriteAsync(string actor, string action, Guid? mediaKey = null, Guid? personId = null, string? detail = null);

    /// <summary>Newest first: entries about one person, optionally limited to the given actions.</summary>
    Task<IReadOnlyList<AuditRow>> ListForPersonAsync(Guid personId, IReadOnlyCollection<string>? actions, int take);

}

public class AuditService(IAuditRepository repository) : IAuditService
{
    public Task WriteAsync(string actor, string action, Guid? mediaKey = null, Guid? personId = null, string? detail = null) =>
        repository.WriteAsync(new AuditRow
        {
            WhenUtc = DateTime.UtcNow,
            ActorKey = actor,
            Action = action,
            MediaKey = mediaKey,
            PersonId = personId,
            Detail = detail
        });

    public Task<IReadOnlyList<AuditRow>> ListForPersonAsync(Guid personId, IReadOnlyCollection<string>? actions, int take) =>
        repository.ListForPersonAsync(personId, actions, take);
}
