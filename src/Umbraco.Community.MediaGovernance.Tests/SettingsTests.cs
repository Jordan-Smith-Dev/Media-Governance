using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeKeyValues : IKeyValueService
{
    public Dictionary<string, string> Values { get; } = new();
    public string? GetValue(string key) => Values.GetValueOrDefault(key);
    public IReadOnlyDictionary<string, string?>? FindByKeyPrefix(string keyPrefix) => throw new NotImplementedException();
    public void SetValue(string key, string value) => Values[key] = value;
    public void SetValue(string key, string originValue, string newValue) => Values[key] = newValue;
    public bool TrySetValue(string key, string originValue, string newValue) { Values[key] = newValue; return true; }
}

public class SettingsServiceTests
{
    private static SettingsService Sut(FakeKeyValues kv, MediaGovernanceOptions? app = null) =>
        new(kv, Options.Create(app ?? new MediaGovernanceOptions()));

    [Fact]
    public async Task With_nothing_saved_everything_comes_from_appsettings()
    {
        var o = await Sut(new FakeKeyValues(), new MediaGovernanceOptions { RetentionYears = 5, WarningDays = 30, BlockWithheldMediaFiles = false, HideWithheldInPages = false }).GetAsync();
        Assert.Equal(5, o.RetentionYears);
        Assert.Equal(30, o.WarningDays);
        Assert.False(o.BlockWithheldFiles);
        Assert.False(o.HideWithheldInPages);
        Assert.Empty(o.NotificationEmails);
    }

    [Fact]
    public async Task Retention_and_warning_saved_in_the_cms_override_appsettings()
    {
        var sut = Sut(new FakeKeyValues(), new MediaGovernanceOptions { RetentionYears = 3, WarningDays = 90 });
        await sut.SaveAsync(new SettingsUpdate(7, 14, ["a@x.com"]));
        var o = await sut.GetAsync();
        Assert.Equal(7, o.RetentionYears);
        Assert.Equal(14, o.WarningDays);
        Assert.Equal(["a@x.com"], o.NotificationEmails);
    }

    [Fact]
    public async Task A_value_saved_equal_to_appsettings_keeps_following_appsettings()
    {
        var kv = new FakeKeyValues();
        await Sut(kv, new MediaGovernanceOptions { RetentionYears = 3 }).SaveAsync(new SettingsUpdate(3, 90, []));
        // appsettings changes later: the unchanged value follows it
        Assert.Equal(6, (await Sut(kv, new MediaGovernanceOptions { RetentionYears = 6 }).GetAsync()).RetentionYears);
    }

    [Fact]
    public async Task The_media_tree_is_left_as_normal_unless_appsettings_turns_the_extras_on()
    {
        var off = await Sut(new FakeKeyValues()).GetAsync();
        Assert.False(off.ShowStatusSigns);
        Assert.False(off.ColourTreeIcons);
        Assert.False(off.ShowFolderMarkers);

        var on = await Sut(new FakeKeyValues(), new MediaGovernanceOptions { ShowStatusSigns = true, ColourTreeIcons = true, ShowFolderMarkers = true }).GetAsync();
        Assert.True(on.ShowStatusSigns);
        Assert.True(on.ColourTreeIcons);
        Assert.True(on.ShowFolderMarkers);
    }

    [Fact]
    public async Task Saving_settings_in_the_cms_does_not_change_the_media_tree_options()
    {
        var sut = Sut(new FakeKeyValues(), new MediaGovernanceOptions { ShowStatusSigns = true });
        await sut.SaveAsync(new SettingsUpdate(3, 90, []));
        Assert.True((await sut.GetAsync()).ShowStatusSigns);
    }

    [Fact]
    public async Task Invalid_settings_are_not_saved()
    {
        var kv = new FakeKeyValues();
        await Assert.ThrowsAsync<ArgumentException>(() => Sut(kv).SaveAsync(new SettingsUpdate(0, 90, [])));
        Assert.Empty(kv.Values);
    }

    [Fact]
    public async Task Garbled_stored_settings_fall_back_to_appsettings()
    {
        var kv = new FakeKeyValues();
        kv.Values["Umbraco.Community.MediaGovernance.Settings"] = "not json";
        Assert.Equal(3, (await Sut(kv).GetAsync()).RetentionYears);
    }

    [Fact]
    public async Task Settings_saved_by_an_earlier_version_still_load()
    {
        var kv = new FakeKeyValues();
        kv.Values["Umbraco.Community.MediaGovernance.Settings"] =
            "{\"RetentionYears\":5,\"WarningDays\":60,\"PermittedUses\":[\"Web\"],\"NotificationEmails\":[\"a@x.com\"]}";
        var o = await Sut(kv).GetAsync();
        Assert.Equal(5, o.RetentionYears);
        Assert.Equal(["a@x.com"], o.NotificationEmails);
    }
}

public class GovernanceAccessTests
{
    [Fact]
    public void With_no_groups_configured_only_sensitive_data_has_access() =>
        Assert.Equal(["sensitiveData"], GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions()));

    [Fact]
    public void Configured_groups_replace_the_default() =>
        Assert.Equal(["admin", "editors"], GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions { AllowedUserGroups = ["admin", " editors "] }));

    [Fact]
    public void Blank_entries_are_ignored_and_an_all_blank_list_is_still_restricted()
    {
        Assert.Equal(["admin"], GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions { AllowedUserGroups = ["", "admin"] }));
        Assert.Empty(GovernanceAccessChecker.GroupsFor(new MediaGovernanceOptions { AllowedUserGroups = [" "] }));
    }

    [Fact]
    public void Nobody_is_allowed_when_there_is_no_user() =>
        Assert.False(new GovernanceAccessChecker(Options.Create(new MediaGovernanceOptions())).IsAllowed(null));
}
