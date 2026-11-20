using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Services.Images;

internal class EntityMediaUploadDates : IMediaUploadDates
{
	private const int ChunkSize = 500;

	private readonly IEntityService _entities;

	public EntityMediaUploadDates(IEntityService entities)
	{
		_entities = entities;
	}

	public Task<IReadOnlyDictionary<Guid, DateOnly>> GetAsync(IReadOnlyCollection<Guid> mediaKeys)
	{
		var result = new Dictionary<Guid, DateOnly>();
		foreach (var chunk in mediaKeys.Distinct().Chunk(ChunkSize))
		{
			foreach (var entity in _entities.GetAll(UmbracoObjectTypes.Media, chunk))
				result[entity.Key] = DateOnly.FromDateTime(entity.CreateDate);
		}
		return Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(result);
	}
}
