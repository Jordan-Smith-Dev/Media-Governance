namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal interface IMediaFileRepository
{
	Task<Guid?> FindKeyAsync(string normalizedPath);

	/// <summary>Keys of every media item whose file path is already recorded.</summary>
	Task<IReadOnlyCollection<Guid>> ListKeysAsync();
	/// <summary>The recorded file path of every media item that has one.</summary>
	Task<IReadOnlyDictionary<Guid, string>> ListPathsAsync();
	Task UpsertAsync(Guid mediaKey, string normalizedPath);
	Task DeleteAsync(Guid mediaKey);
}
