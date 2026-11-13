using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class FrontEndPolicyTests
{
    private static readonly GovernanceOptions Unset = new(3, 90, ["Web"], []);

    [Fact]
    public void Until_chosen_files_follow_appsettings_and_pages_hide()
    {
        Assert.Equal(new FrontEndPolicy(true, true), FrontEndPolicy.Resolve(Unset, blockFilesDefault: true));
        Assert.Equal(new FrontEndPolicy(false, true), FrontEndPolicy.Resolve(Unset, blockFilesDefault: false));
    }

    [Fact]
    public void A_choice_saved_in_the_dashboard_beats_appsettings()
    {
        var saved = Unset with { BlockWithheldFiles = false, HideWithheldInPages = false };
        Assert.Equal(new FrontEndPolicy(false, false), FrontEndPolicy.Resolve(saved, blockFilesDefault: true));
        Assert.Equal(new FrontEndPolicy(true, true), FrontEndPolicy.Resolve(saved with { BlockWithheldFiles = true, HideWithheldInPages = true }, blockFilesDefault: false));
    }

    [Fact]
    public void Settings_saved_before_these_options_existed_still_load()
    {
        var old = "{\"RetentionYears\":3,\"WarningDays\":90,\"PermittedUses\":[\"Web\"],\"NotificationEmails\":[]}";
        var options = JsonSerializer.Deserialize<GovernanceOptions>(old)!;
        Assert.Null(options.BlockWithheldFiles);
        Assert.Null(options.HideWithheldInPages);
    }

    [Fact]
    public async Task The_provider_reads_settings_and_caches_until_cleared()
    {
        var settings = new FakeSettings { Options = Unset };
        var cache = new MediaGateCache();
        var sut = new FrontEndPolicyProvider(settings, Options.Create(new MediaGovernanceOptions { BlockWithheldMediaFiles = true }), cache);

        Assert.True((await sut.GetAsync()).BlockFiles);

        settings.Options = Unset with { BlockWithheldFiles = false };
        Assert.True((await sut.GetAsync()).BlockFiles);   // cached

        cache.Clear();                                     // what saving settings does
        Assert.False((await sut.GetAsync()).BlockFiles);
    }
}
