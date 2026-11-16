namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>What the public website does with images that are withheld (Expired, or without consent).</summary>
/// <param name="BlockFiles">Requests for the image file return 404.</param>
/// <param name="HidePages">The &lt;media-governance&gt; tag helper leaves the image out of pages.</param>
public record FrontEndPolicy(bool BlockFiles, bool HidePages);

public interface IFrontEndPolicyProvider
{
    Task<FrontEndPolicy> GetAsync();
}

/// <summary>Reads the policy for every request to /media and every wrapped image, so it is cached briefly.</summary>
public class FrontEndPolicyProvider(ISettingsService settings, MediaGateCache cache) : IFrontEndPolicyProvider
{
    private const string CacheKey = "mg-front-end-policy";

    public async Task<FrontEndPolicy> GetAsync()
    {
        if (cache.TryGet<FrontEndPolicy>(CacheKey, out var cached) && cached is not null)
            return cached;

        var options = await settings.GetAsync();
        var policy = new FrontEndPolicy(options.BlockWithheldFiles, options.HideWithheldInPages);
        cache.Set(CacheKey, policy);
        return policy;
    }
}
