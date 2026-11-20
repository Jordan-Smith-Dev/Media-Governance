using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Middleware;

namespace Umbraco.Community.MediaGovernance.Services.Settings;

internal class SettingsService : ISettingsService
{
	private const string Key = "Umbraco.Community.MediaGovernance.Settings";

	private readonly IKeyValueService _keyValues;
	private readonly IOptions<MediaGovernanceOptions> _app;

	public SettingsService(IKeyValueService keyValues, IOptions<MediaGovernanceOptions> app)
	{
		_keyValues = keyValues;
		_app = app;
	}

	public MediaGovernanceOptions AppSettings => _app.Value;

	public Task<GovernanceOptions> GetAsync()
	{
		var stored = Read();
		var a = _app.Value;
		// Admin overrides only count while the switch is on. If it is switched off they are kept, but ignored.
		var admin = a.AllowAdminSettings ? stored?.Admin : null;
		return Task.FromResult(new GovernanceOptions(
			stored?.RetentionYears ?? a.RetentionYears,
			stored?.WarningDays ?? a.WarningDays,
			stored?.NotificationEmails ?? [],
			stored?.NotificationUserGroupIds,
			admin?.BlockWithheldFiles ?? a.BlockWithheldMediaFiles,
			admin?.HideWithheldInPages ?? a.HideWithheldInPages,
			admin?.ShowStatusSigns ?? a.ShowStatusSigns,
			admin?.ColourTreeIcons ?? a.ColourTreeIcons,
			admin?.ShowFolderMarkers ?? a.ShowFolderMarkers,
			admin?.ConsentDetails ?? a.ConsentDetails,
			admin?.ConsentExpiry ?? a.ConsentExpiry,
			admin?.ConsentEvidence ?? a.ConsentEvidence,
			admin?.PermittedUses ?? AdminSettingsValidator.CleanUses(a.PermittedUses)));
	}

	public async Task<GovernanceOptions> SaveAsync(SettingsUpdate update)
	{
		var cleaned = GovernanceOptionsValidator.Normalize(update);
		GovernanceOptionsValidator.Validate(cleaned);

		var a = _app.Value;
		var stored = new StoredSettings(
			cleaned.RetentionYears == a.RetentionYears ? null : cleaned.RetentionYears,
			cleaned.WarningDays == a.WarningDays ? null : cleaned.WarningDays,
			cleaned.NotificationEmails,
			cleaned.NotificationUserGroupIds,
			Read()?.Admin);   // saving these never touches the admin overrides
		_keyValues.SetValue(Key, JsonSerializer.Serialize(stored));
		return await GetAsync();
	}

	public async Task<GovernanceOptions> SaveAdminAsync(AdminSettingsUpdate update)
	{
		var a = _app.Value;
		if (!a.AllowAdminSettings)
			throw new InvalidOperationException("Admin settings are not switched on. Set AllowAdminSettings to true in appsettings.");

		var cleaned = AdminSettingsValidator.Normalize(update);
		AdminSettingsValidator.Validate(cleaned);

		var defaultUses = AdminSettingsValidator.CleanUses(a.PermittedUses);
		var sameUses = cleaned.PermittedUses.Count == defaultUses.Count
			&& cleaned.PermittedUses.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(defaultUses.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

		var overrides = new AdminOverrides(
			Differs(cleaned.BlockWithheldFiles, a.BlockWithheldMediaFiles),
			Differs(cleaned.HideWithheldInPages, a.HideWithheldInPages),
			Differs(cleaned.ShowStatusSigns, a.ShowStatusSigns),
			Differs(cleaned.ColourTreeIcons, a.ColourTreeIcons),
			Differs(cleaned.ShowFolderMarkers, a.ShowFolderMarkers),
			Differs(cleaned.ConsentDetails, a.ConsentDetails),
			Differs(cleaned.ConsentExpiry, a.ConsentExpiry),
			Differs(cleaned.ConsentEvidence, a.ConsentEvidence),
			sameUses ? null : cleaned.PermittedUses);

		var existing = Read() ?? new StoredSettings(null, null, null, null);
		_keyValues.SetValue(Key, JsonSerializer.Serialize(existing with { Admin = overrides.IsEmpty ? null : overrides }));
		return await GetAsync();
	}

	private static bool? Differs(bool value, bool appsettings) => value == appsettings ? null : value;

	private StoredSettings? Read()
	{
		var json = _keyValues.GetValue(Key);
		if (string.IsNullOrWhiteSpace(json))
			return null;
		try
		{
			return JsonSerializer.Deserialize<StoredSettings>(json);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
