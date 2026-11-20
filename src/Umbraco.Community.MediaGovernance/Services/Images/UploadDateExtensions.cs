using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.Images;

internal static class UploadDateExtensions
{
	/// <summary>Fills in each image's upload date (when a source is available) so retention can start from it.</summary>
	public static async Task<IReadOnlyList<ImageGovernanceFacts>> WithUploadDatesAsync(
		this Task<IReadOnlyList<ImageGovernanceFacts>> factsTask, IMediaUploadDates? uploads)
	{
		var facts = await factsTask;
		if (uploads is null || facts.Count == 0)
			return facts;

		var dates = await uploads.GetAsync(facts.Select(f => f.MediaKey).ToList());
		return facts.Select(f => dates.TryGetValue(f.MediaKey, out var d) ? f with { DateUploaded = d } : f).ToList();
	}
}
