using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Middleware;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>What the public website does with images that are withheld (Expired, or without consent).</summary>
/// <param name="BlockFiles">Requests for the image file return 404.</param>
/// <param name="HidePages">The &lt;media-governance&gt; tag helper leaves the image out of pages.</param>
public record FrontEndPolicy(bool BlockFiles, bool HidePages)
{
    /// <summary>
    /// A choice saved in the dashboard wins. Until one is saved, files follow the appsettings value
    /// (blocked unless turned off there) and pages hide withheld images.
    /// </summary>
    public static FrontEndPolicy Resolve(GovernanceOptions saved, bool blockFilesDefault) =>
        new(saved.BlockWithheldFiles ?? blockFilesDefault, saved.HideWithheldInPages ?? true);
}

public interface IFrontEndPolicyProvider
{
    Task<FrontEndPolicy> GetAsync();
}

/// <summary>Reads the policy for every request to /media and every wrapped image, so it is cached briefly. Saving settings clears the cache.</summary>
public class FrontEndPolicyProvider(ISettingsService settings, IOptions<MediaGovernanceOptions> appSettings, MediaGateCache cache) : IFrontEndPolicyProvider
{
    private const string CacheKey = "mg-front-end-policy";

    public async Task<FrontEndPolicy> GetAsync()
    {
        if (cache.TryGet<FrontEndPolicy>(CacheKey, out var cached) && cached is not null)
            return cached;

        var policy = FrontEndPolicy.Resolve(await settings.GetAsync(), appSettings.Value.BlockWithheldMediaFiles);
        cache.Set(CacheKey, policy);
        return policy;
    }
}
