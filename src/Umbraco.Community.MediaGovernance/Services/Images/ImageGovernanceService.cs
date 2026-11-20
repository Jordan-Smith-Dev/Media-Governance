using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services.Audit;

namespace Umbraco.Community.MediaGovernance.Services.Images;

public class ImageGovernanceService
{
	private readonly IImageRepository _images;
	private readonly IPersonRepository _people;
	private readonly IAuditService _audit;

	public ImageGovernanceService(IImageRepository images, IPersonRepository people, IAuditService audit)
	{
		_images = images;
		_people = people;
		_audit = audit;
	}

	public async Task<(ImageRow Image, IReadOnlyList<Guid> TaggedPersonIds)> GetAsync(Guid mediaKey)
	{
		var row = await _images.GetAsync(mediaKey) ?? new ImageRow { MediaKey = mediaKey };
		return (row, await _images.GetTaggedPersonIdsAsync(mediaKey));
	}

	public async Task SaveAsync(Guid mediaKey, ImageGovernanceUpdate update, string actor)
	{
		foreach (var personId in update.TaggedPersonIds.Distinct())
		{
			if (await _people.GetAsync(personId) is null)
				throw new ArgumentException($"Person {personId} does not exist (they may have been erased).", nameof(update));
		}

		var row = new ImageRow
		{
			MediaKey = mediaKey,
			DateTaken = Dates.ToDb(update.DateTaken),
			DateObtained = Dates.ToDb(update.DateObtained),
			CustomExpiry = Dates.ToDb(update.CustomExpiry),
			NoPeople = update.NoPeople
		};
		await _images.SaveAsync(row, update.TaggedPersonIds);
		await _audit.WriteAsync(actor, "image.updated", mediaKey);
	}

	public const int MaxBulk = 500;

	/// <summary>
	/// Applies one change to many images, keeping each image's other details and tags as they are.
	/// "No people" is skipped for any image that already has someone tagged.
	/// </summary>
	public async Task<BulkImageResult> BulkAsync(IEnumerable<Guid> mediaKeys, BulkImageChange change, string actor)
	{
		var chosen = new[] { change.NoPeople, change.CustomExpiry is not null, change.ClearCustomExpiry }.Count(c => c);
		if (chosen != 1)
			throw new ArgumentException("Choose exactly one change: mark as no people, set an expiry date, or clear the expiry date.", nameof(change));

		var keys = mediaKeys.Distinct().ToList();
		if (keys.Count == 0)
			throw new ArgumentException("Choose at least one image.", nameof(mediaKeys));
		if (keys.Count > MaxBulk)
			throw new ArgumentException($"Choose {MaxBulk} images or fewer at a time.", nameof(mediaKeys));

		var updated = new List<Guid>();
		var skipped = new List<Guid>();
		foreach (var key in keys)
		{
			var row = await _images.GetAsync(key) ?? new ImageRow { MediaKey = key };
			var tagged = await _images.GetTaggedPersonIdsAsync(key);

			if (change.NoPeople)
			{
				if (tagged.Count > 0)
				{ skipped.Add(key); continue; }   // someone is tagged, so "no people" would be wrong
				row.NoPeople = true;
			}
			else
			{
				row.CustomExpiry = change.ClearCustomExpiry ? null : Dates.ToDb(change.CustomExpiry);
			}

			await _images.SaveAsync(row, tagged);
			await _audit.WriteAsync(actor, "image.updated", key);
			updated.Add(key);
		}
		return new BulkImageResult(updated, skipped);
	}
}
