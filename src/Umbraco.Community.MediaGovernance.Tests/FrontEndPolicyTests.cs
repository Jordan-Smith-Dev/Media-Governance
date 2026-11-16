using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class FrontEndPolicyTests
{
    [Fact]
    public async Task The_provider_follows_the_settings_and_caches_until_cleared()
    {
        var settings = new FakeSettings { Options = new(3, 90, []) };
        var cache = new MediaGateCache();
        var sut = new FrontEndPolicyProvider(settings, cache);

        Assert.Equal(new FrontEndPolicy(true, true), await sut.GetAsync());

        settings.Options = settings.Options with { BlockWithheldFiles = false, HideWithheldInPages = false };
        Assert.True((await sut.GetAsync()).BlockFiles);   // cached

        cache.Clear();
        Assert.Equal(new FrontEndPolicy(false, false), await sut.GetAsync());
    }
}
