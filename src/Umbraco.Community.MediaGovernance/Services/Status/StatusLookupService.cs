using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.Settings;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.Status;

/// <summary>Status for a specific set of images (cheaper than evaluating the whole library, e.g. for tree pages).</summary>
public class StatusLookupService
{
	private readonly IImageRepository _images;
	private readonly ISettingsService _settings;
	private readonly TimeProvider _time;
	private readonly IMediaUploadDates? _uploads;

	public StatusLookupService(IImageRepository images, ISettingsService settings, TimeProvider time, IMediaUploadDates? uploads = null)
	{
		_images = images;
		_settings = settings;
		_time = time;
		_uploads = uploads;
	}

	/// <summary>Every problem for each image (empty list when compliant), most severe first.</summary>
	public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<GovernanceStatus>>> GetIssuesAsync(IReadOnlyCollection<Guid> mediaKeys)
	{
		if (mediaKeys.Count == 0)
			return new Dictionary<Guid, IReadOnlyList<GovernanceStatus>>();

		var options = (await _settings.GetAsync()).ToCore();
		var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
		var facts = await _images.GetFactsAsync(mediaKeys).WithUploadDatesAsync(_uploads);
		return facts.ToDictionary(f => f.MediaKey, f => StatusCalculator.Issues(f, options, today));
	}

	public async Task<IReadOnlyDictionary<Guid, GovernanceStatus>> GetStatusesAsync(IReadOnlyCollection<Guid> mediaKeys)
	{
		if (mediaKeys.Count == 0)
			return new Dictionary<Guid, GovernanceStatus>();

		var options = (await _settings.GetAsync()).ToCore();
		var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
		var facts = await _images.GetFactsAsync(mediaKeys).WithUploadDatesAsync(_uploads);
		return facts.ToDictionary(f => f.MediaKey, f => StatusCalculator.Calculate(f, options, today));
	}
}
