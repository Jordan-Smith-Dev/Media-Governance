using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

public interface IImageRepository
{
	Task<ImageRow?> GetAsync(Guid mediaKey);
	Task<IReadOnlyList<Guid>> GetTaggedPersonIdsAsync(Guid mediaKey);
	Task SaveAsync(ImageRow image, IReadOnlyList<Guid> taggedPersonIds);

	Task DeleteForMediaAsync(Guid mediaKey);

	/// <summary>Keys of the images a person is tagged in.</summary>
	Task<IReadOnlyList<Guid>> ListImageKeysForPersonAsync(Guid personId);

	/// <summary>Facts for the given media keys (keys with no governance data yield empty facts).</summary>
	Task<IReadOnlyList<ImageGovernanceFacts>> GetFactsAsync(IReadOnlyCollection<Guid> mediaKeys);

	/// <summary>How many images each person is tagged in.</summary>
	Task<IReadOnlyDictionary<Guid, int>> CountTagsByPersonAsync();

	/// <summary>Keys of every image that has governance data.</summary>
	Task<IReadOnlyList<Guid>> ListGovernedKeysAsync();
}
