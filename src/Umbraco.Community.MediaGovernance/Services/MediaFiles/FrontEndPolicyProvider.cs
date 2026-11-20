using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

/// <summary>Reads the policy for every request to /media and every wrapped image, so it is cached briefly.</summary>
internal class FrontEndPolicyProvider : IFrontEndPolicyProvider
{
	private const string CacheKey = "mg-front-end-policy";

	private readonly ISettingsService _settings;
	private readonly MediaGateCache _cache;

	public FrontEndPolicyProvider(ISettingsService settings, MediaGateCache cache)
	{
		_settings = settings;
		_cache = cache;
	}

	public async Task<FrontEndPolicy> GetAsync()
	{
		if (_cache.TryGet<FrontEndPolicy>(CacheKey, out var cached) && cached is not null)
			return cached;

		var options = await _settings.GetAsync();
		var policy = new FrontEndPolicy(options.BlockWithheldFiles, options.HideWithheldInPages);
		_cache.Set(CacheKey, policy);
		return policy;
	}
}
