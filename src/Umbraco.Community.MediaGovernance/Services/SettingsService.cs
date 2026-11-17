using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Middleware;

namespace Umbraco.Community.MediaGovernance.Services;

public interface ISettingsService
{
    /// <summary>What is in force now: appsettings, with CMS overrides applied.</summary>
    Task<GovernanceOptions> GetAsync();

    /// <summary>The values from appsettings, before any CMS override.</summary>
    MediaGovernanceOptions AppSettings { get; }

    /// <summary>Cleans, validates and stores the CMS-editable settings; returns what is now in force.</summary>
    Task<GovernanceOptions> SaveAsync(SettingsUpdate update);
}

public class SettingsService(IKeyValueService keyValues, IOptions<MediaGovernanceOptions> app) : ISettingsService
{
    private const string Key = "Umbraco.Community.MediaGovernance.Settings";

    public MediaGovernanceOptions AppSettings => app.Value;

    public Task<GovernanceOptions> GetAsync()
    {
        var stored = Read();
        var a = app.Value;
        return Task.FromResult(new GovernanceOptions(
            stored?.RetentionYears ?? a.RetentionYears,
            stored?.WarningDays ?? a.WarningDays,
            stored?.NotificationEmails ?? [],
            stored?.NotificationUserGroupIds,
            a.BlockWithheldMediaFiles,
            a.HideWithheldInPages,
            a.ShowStatusSigns,
            a.ColourTreeIcons,
            a.ShowFolderMarkers,
            a.ConsentDetails,
            a.ConsentExpiry,
            a.ConsentEvidence,
            a.PermittedUses.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
    }

    public async Task<GovernanceOptions> SaveAsync(SettingsUpdate update)
    {
        var cleaned = GovernanceOptionsValidator.Normalize(update);
        GovernanceOptionsValidator.Validate(cleaned);

        var a = app.Value;
        var stored = new StoredSettings(
            cleaned.RetentionYears == a.RetentionYears ? null : cleaned.RetentionYears,
            cleaned.WarningDays == a.WarningDays ? null : cleaned.WarningDays,
            cleaned.NotificationEmails,
            cleaned.NotificationUserGroupIds);
        keyValues.SetValue(Key, JsonSerializer.Serialize(stored));
        return await GetAsync();
    }

    private StoredSettings? Read()
    {
        var json = keyValues.GetValue(Key);
        if (string.IsNullOrWhiteSpace(json)) return null;
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
