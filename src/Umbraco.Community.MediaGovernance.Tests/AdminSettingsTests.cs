using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Controllers;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Tests;

public class AdminSettingsTests
{
	private const string Key = "Umbraco.Community.MediaGovernance.Settings";

	private static SettingsService Sut(FakeKeyValues kv, MediaGovernanceOptions? app = null) =>
		new(kv, Options.Create(app ?? new MediaGovernanceOptions()));

	private static MediaGovernanceOptions On(Action<MediaGovernanceOptions>? tweak = null)
	{
		var o = new MediaGovernanceOptions { AllowAdminSettings = true };
		tweak?.Invoke(o);
		return o;
	}

	private static AdminSettingsUpdate Same(MediaGovernanceOptions a) => new(
		a.BlockWithheldMediaFiles, a.HideWithheldInPages, a.ShowStatusSigns, a.ColourTreeIcons, a.ShowFolderMarkers,
		a.ConsentDetails, a.ConsentExpiry, a.ConsentEvidence, a.PermittedUses);

	// ---------------------------------------------------------------- off by default

	[Fact]
	public void Admin_settings_are_off_by_default_and_the_admin_group_is_the_default_admin()
	{
		var o = new MediaGovernanceOptions();
		Assert.False(o.AllowAdminSettings);
		Assert.Empty(o.AdminSettingsUserGroups);
		Assert.Equal(["admin"], GovernanceAccessChecker.AdminGroupsFor(o));
	}

	[Fact]
	public async Task Nothing_can_be_saved_while_the_switch_is_off()
	{
		var kv = new FakeKeyValues();
		await Assert.ThrowsAsync<InvalidOperationException>(() => Sut(kv).SaveAdminAsync(Same(new MediaGovernanceOptions()) with { BlockWithheldFiles = false }));
		Assert.Empty(kv.Values);
	}

	[Fact]
	public void Nobody_is_an_admin_without_a_user()
	{
		Assert.False(new GovernanceAccessChecker(Options.Create(On())).IsAdmin(null));
		Assert.False(new GovernanceAccessChecker(Options.Create(new MediaGovernanceOptions())).IsAdmin(null));
	}

	[Fact]
	public void Admin_groups_come_from_appsettings_and_blank_entries_are_ignored()
	{
		Assert.Equal(["editors"], GovernanceAccessChecker.AdminGroupsFor(new MediaGovernanceOptions { AdminSettingsUserGroups = ["", " editors "] }));
		Assert.Equal(["admin"], GovernanceAccessChecker.AdminGroupsFor(new MediaGovernanceOptions { AdminSettingsUserGroups = [] }));
	}

	// ---------------------------------------------------------------- overrides

	[Fact]
	public async Task An_override_beats_appsettings_while_the_switch_is_on()
	{
		var kv = new FakeKeyValues();
		var sut = Sut(kv, On(o => o.PermittedUses = ["Web"]));
		var saved = await sut.SaveAdminAsync(new AdminSettingsUpdate(false, false, true, true, true, true, true, true, ["Web", "Print"]));
		Assert.False(saved.BlockWithheldFiles);
		Assert.False(saved.HideWithheldInPages);
		Assert.True(saved.ShowStatusSigns);
		Assert.True(saved.ColourTreeIcons);
		Assert.True(saved.ShowFolderMarkers);
		Assert.True(saved.ConsentDetails);
		Assert.True(saved.ConsentExpiry);
		Assert.True(saved.ConsentEvidence);
		Assert.Equal(["Web", "Print"], saved.PermittedUses);
		Assert.Equal(saved.PermittedUses, (await sut.GetAsync()).PermittedUses);
	}

	[Fact]
	public async Task Overrides_are_ignored_but_kept_when_the_switch_is_turned_off_and_return_when_it_is_turned_on()
	{
		var kv = new FakeKeyValues();
		await Sut(kv, On()).SaveAdminAsync(Same(On()) with { BlockWithheldFiles = false, ConsentEvidence = true });

		var off = await Sut(kv, new MediaGovernanceOptions()).GetAsync();
		Assert.True(off.BlockWithheldFiles);      // appsettings wins again
		Assert.False(off.ConsentEvidence);

		var on = await Sut(kv, On()).GetAsync();
		Assert.False(on.BlockWithheldFiles);      // what the admin saved is still there
		Assert.True(on.ConsentEvidence);
	}

	[Fact]
	public async Task A_value_equal_to_appsettings_keeps_following_appsettings()
	{
		var kv = new FakeKeyValues();
		await Sut(kv, On()).SaveAdminAsync(Same(On()));
		Assert.Null(JsonSerializer.Deserialize<StoredSettings>(kv.Values[Key])!.Admin);

		// appsettings changes later, and the unchanged value follows it
		Assert.False((await Sut(kv, On(o => o.BlockWithheldMediaFiles = false)).GetAsync()).BlockWithheldFiles);
	}

	[Fact]
	public async Task Only_the_values_that_differ_are_stored()
	{
		var kv = new FakeKeyValues();
		await Sut(kv, On()).SaveAdminAsync(Same(On()) with { ColourTreeIcons = true });
		var admin = JsonSerializer.Deserialize<StoredSettings>(kv.Values[Key])!.Admin!;
		Assert.True(admin.ColourTreeIcons);
		Assert.Null(admin.BlockWithheldFiles);
		Assert.Null(admin.PermittedUses);
	}

	[Fact]
	public async Task Resetting_to_appsettings_clears_the_override()
	{
		var kv = new FakeKeyValues();
		var sut = Sut(kv, On());
		await sut.SaveAdminAsync(Same(On()) with { ConsentExpiry = true });
		await sut.SaveAdminAsync(Same(On()));
		Assert.False((await sut.GetAsync()).ConsentExpiry);
		Assert.Null(JsonSerializer.Deserialize<StoredSettings>(kv.Values[Key])!.Admin);
	}

	[Fact]
	public async Task Saving_retention_does_not_touch_the_admin_overrides_and_the_reverse()
	{
		var kv = new FakeKeyValues();
		var sut = Sut(kv, On());
		await sut.SaveAdminAsync(Same(On()) with { ShowStatusSigns = true });
		await sut.SaveAsync(new SettingsUpdate(7, 30, ["ops@example.com"]));
		var o = await sut.GetAsync();
		Assert.True(o.ShowStatusSigns);
		Assert.Equal(7, o.RetentionYears);

		await sut.SaveAdminAsync(Same(On()) with { ShowStatusSigns = true, BlockWithheldFiles = false });
		var again = await sut.GetAsync();
		Assert.Equal(7, again.RetentionYears);
		Assert.Equal(["ops@example.com"], again.NotificationEmails);
	}

	// ---------------------------------------------------------------- permitted uses

	[Fact]
	public async Task Permitted_uses_are_trimmed_and_deduplicated_keeping_the_first_spelling()
	{
		var saved = await Sut(new FakeKeyValues(), On()).SaveAdminAsync(Same(On()) with { PermittedUses = [" Web ", "web", "Print", ""] });
		Assert.Equal(["Web", "Print"], saved.PermittedUses);
	}

	[Fact]
	public async Task The_same_uses_in_another_order_or_case_are_not_an_override()
	{
		var kv = new FakeKeyValues();
		var app = On(o => o.PermittedUses = ["Web", "Print"]);
		await Sut(kv, app).SaveAdminAsync(Same(app) with { PermittedUses = ["print", "WEB"] });
		Assert.Null(JsonSerializer.Deserialize<StoredSettings>(kv.Values[Key])!.Admin);
	}

	[Fact]
	public async Task Clearing_every_use_turns_per_use_consent_off()
	{
		var saved = await Sut(new FakeKeyValues(), On(o => o.PermittedUses = ["Web"])).SaveAdminAsync(Same(On()) with { PermittedUses = [] });
		Assert.False(saved.UsesOn);
	}

	[Theory]
	[InlineData("a,b")]
	[InlineData("two\nlines")]
	public async Task A_use_cannot_contain_a_comma_or_a_line_break(string use) =>
		await Assert.ThrowsAsync<ArgumentException>(() => Sut(new FakeKeyValues(), On()).SaveAdminAsync(Same(On()) with { PermittedUses = [use] }));

	[Fact]
	public async Task A_use_longer_than_30_characters_is_refused()
	{
		var kv = new FakeKeyValues();
		await Assert.ThrowsAsync<ArgumentException>(() => Sut(kv, On()).SaveAdminAsync(Same(On()) with { PermittedUses = [new string('x', 31)] }));
		Assert.Empty(kv.Values);
	}

	[Fact]
	public async Task More_than_12_uses_are_refused()
	{
		var many = Enumerable.Range(1, 13).Select(i => $"Use {i}").ToArray();
		await Assert.ThrowsAsync<ArgumentException>(() => Sut(new FakeKeyValues(), On()).SaveAdminAsync(Same(On()) with { PermittedUses = many }));
		var twelve = Enumerable.Range(1, 12).Select(i => $"Use {i}").ToArray();
		var saved = await Sut(new FakeKeyValues(), On()).SaveAdminAsync(Same(On()) with { PermittedUses = twelve });
		Assert.Equal(12, saved.PermittedUses!.Count);
	}

	// ---------------------------------------------------------------- the audit text

	[Fact]
	public void Nothing_changed_means_nothing_to_audit()
	{
		var o = GovernanceOptions.Default;
		Assert.Empty(ImageController.AdminChanges(o, o));
	}

	[Fact]
	public void The_audit_lists_each_change_with_the_old_and_new_value_and_nothing_else()
	{
		var before = GovernanceOptions.Default with { BlockWithheldFiles = true, ConsentEvidence = false, PermittedUses = ["Web", "Print"] };
		var after = before with { BlockWithheldFiles = false, ConsentEvidence = true, PermittedUses = ["Web"] };
		Assert.Equal(
			["blockWithheldFiles True -> False", "consentEvidence False -> True", "permittedUses [Web, Print] -> [Web]"],
			ImageController.AdminChanges(before, after));
	}

	[Fact]
	public void Settings_saved_before_admin_settings_existed_still_load()
	{
		var kv = new FakeKeyValues();
		kv.Values[Key] = "{\"RetentionYears\":5,\"WarningDays\":60,\"NotificationEmails\":[\"a@x.com\"],\"NotificationUserGroupIds\":null}";
		var o = Sut(kv, On()).GetAsync().Result;
		Assert.Equal(5, o.RetentionYears);
		Assert.True(o.BlockWithheldFiles);
	}
}
