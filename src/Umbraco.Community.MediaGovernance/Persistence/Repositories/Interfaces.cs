using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

public interface IPersonRepository
{
    Task<PersonRow?> GetAsync(Guid id);
    Task<IReadOnlyList<PersonRow>> ListAsync(string? query);
    Task SaveAsync(PersonRow person);

    /// <summary>Deletes the person, their tags and consent records. Returns evidence paths that must be deleted from storage.</summary>
    Task<IReadOnlyList<string>> EraseAsync(Guid personId);
}

public interface IImageRepository
{
    Task<ImageRow?> GetAsync(Guid mediaKey);
    Task<IReadOnlyList<Guid>> GetTaggedPersonIdsAsync(Guid mediaKey);
    Task SaveAsync(ImageRow image, IReadOnlyList<Guid> taggedPersonIds);

    /// <summary>Deletes governance data for a media item. Returns evidence paths that must be deleted from storage.</summary>
    Task<IReadOnlyList<string>> DeleteForMediaAsync(Guid mediaKey);

    /// <summary>Facts for the given media keys (keys with no governance data yield empty facts).</summary>
    Task<IReadOnlyList<ImageGovernanceFacts>> GetFactsAsync(IReadOnlyCollection<Guid> mediaKeys);

    /// <summary>How many images each person is tagged in.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountTagsByPersonAsync();

    /// <summary>Keys of every image that has governance data.</summary>
    Task<IReadOnlyList<Guid>> ListGovernedKeysAsync();
}

public interface IMediaFileRepository
{
    Task<Guid?> FindKeyAsync(string normalizedPath);

    /// <summary>Keys of every media item whose file path is already recorded.</summary>
    Task<IReadOnlyCollection<Guid>> ListKeysAsync();
    Task UpsertAsync(Guid mediaKey, string normalizedPath);
    Task DeleteAsync(Guid mediaKey);
}

public interface IConsentRepository
{
    Task<ConsentRow?> GetAsync(Guid id);
    Task SaveAsync(ConsentRow consent);
    Task<IReadOnlyList<ConsentRow>> ListForImageAsync(Guid mediaKey);
}

public interface IAuditRepository
{
    Task WriteAsync(AuditRow row);
    Task<IReadOnlyList<AuditRow>> ListAsync(int take);
}

public interface INotifiedRepository
{
    Task<IReadOnlyDictionary<Guid, GovernanceStatus>> GetAllAsync();
    Task UpsertAsync(Guid mediaKey, GovernanceStatus status);
}
