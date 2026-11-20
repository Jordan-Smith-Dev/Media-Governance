using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal class MediaFileRepository : IMediaFileRepository
{
	private readonly IScopeProvider _scopeProvider;

	public MediaFileRepository(IScopeProvider scopeProvider)
	{
		_scopeProvider = scopeProvider;
	}

	public async Task<Guid?> FindKeyAsync(string normalizedPath)
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		var row = scope.Database.FirstOrDefault<MediaFileRow>($"SELECT * FROM {TableNames.MediaFile} WHERE FilePath = @0", normalizedPath);
		return row?.MediaKey;
	}

	public async Task<IReadOnlyCollection<Guid>> ListKeysAsync()
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.Fetch<Guid>($"SELECT MediaKey FROM {TableNames.MediaFile}");
	}

	public async Task<IReadOnlyDictionary<Guid, string>> ListPathsAsync()
	{
		using var scope = _scopeProvider.CreateScope(autoComplete: true);
		return scope.Database.Fetch<MediaFileRow>($"SELECT * FROM {TableNames.MediaFile}").ToDictionary(r => r.MediaKey, r => r.FilePath);
	}

	public async Task UpsertAsync(Guid mediaKey, string normalizedPath)
	{
		using var scope = _scopeProvider.CreateScope();
		var row = new MediaFileRow { MediaKey = mediaKey, FilePath = normalizedPath };
		if (scope.Database.Exists<MediaFileRow>(mediaKey))
			scope.Database.Update(row);
		else
			scope.Database.Insert(row);
		scope.Complete();
	}

	public async Task DeleteAsync(Guid mediaKey)
	{
		using var scope = _scopeProvider.CreateScope();
		scope.Database.Execute($"DELETE FROM {TableNames.MediaFile} WHERE MediaKey = @0", mediaKey);
		scope.Complete();
	}
}
