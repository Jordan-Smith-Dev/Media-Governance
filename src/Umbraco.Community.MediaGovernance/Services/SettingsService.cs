using System.Text.Json;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Services;

public interface ISettingsService
{
    Task<GovernanceOptions> GetAsync();
    /// <summary>Cleans, validates and stores the options; returns what was stored.</summary>
    Task<GovernanceOptions> SaveAsync(GovernanceOptions options);
}

public class SettingsService(IKeyValueService keyValues) : ISettingsService
{
    private const string Key = "Umbraco.Community.MediaGovernance.Settings";

    public Task<GovernanceOptions> GetAsync()
    {
        var json = keyValues.GetValue(Key);
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(GovernanceOptions.Default);

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<GovernanceOptions>(json) ?? GovernanceOptions.Default);
        }
        catch (JsonException)
        {
            return Task.FromResult(GovernanceOptions.Default);
        }
    }

    public Task<GovernanceOptions> SaveAsync(GovernanceOptions options)
    {
        var cleaned = GovernanceOptionsValidator.Normalize(options);
        GovernanceOptionsValidator.Validate(cleaned);
        keyValues.SetValue(Key, JsonSerializer.Serialize(cleaned));
        return Task.FromResult(cleaned);
    }
}
