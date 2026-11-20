using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeImageRepository : IImageRepository
{
	public Dictionary<Guid, ImageRow> Images { get; } = new();
	public Dictionary<Guid, List<Guid>> Tags { get; } = new();

	public Task<ImageRow?> GetAsync(Guid mediaKey) => Task.FromResult(Images.GetValueOrDefault(mediaKey));
	public Task<IReadOnlyList<Guid>> GetTaggedPersonIdsAsync(Guid mediaKey) =>
		Task.FromResult<IReadOnlyList<Guid>>(Tags.GetValueOrDefault(mediaKey) ?? new List<Guid>());
	public Task SaveAsync(ImageRow image, IReadOnlyList<Guid> taggedPersonIds)
	{
		Images[image.MediaKey] = image;
		Tags[image.MediaKey] = taggedPersonIds.ToList();
		return Task.CompletedTask;
	}
	public Task DeleteForMediaAsync(Guid mediaKey) => Task.CompletedTask;
	public Task<IReadOnlyList<Guid>> ListImageKeysForPersonAsync(Guid personId) =>
		Task.FromResult<IReadOnlyList<Guid>>(Tags.Where(t => t.Value.Contains(personId)).Select(t => t.Key).ToList());
	public Task<IReadOnlyDictionary<Guid, int>> CountTagsByPersonAsync() =>
		Task.FromResult<IReadOnlyDictionary<Guid, int>>(Tags.Values.SelectMany(t => t).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count()));

	public Task<IReadOnlyList<Guid>> ListGovernedKeysAsync() => Task.FromResult<IReadOnlyList<Guid>>(Images.Keys.ToList());

	public Dictionary<Guid, ImageGovernanceFacts> Facts { get; } = new();
	public Task<IReadOnlyList<ImageGovernanceFacts>> GetFactsAsync(IReadOnlyCollection<Guid> mediaKeys) =>
		Task.FromResult<IReadOnlyList<ImageGovernanceFacts>>(
			mediaKeys.Select(k => Facts.GetValueOrDefault(k) ?? new ImageGovernanceFacts(k, null, null, null, false, [], [])).ToList());
}
