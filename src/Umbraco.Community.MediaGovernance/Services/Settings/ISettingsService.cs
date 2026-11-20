using Umbraco.Community.MediaGovernance.Middleware;

namespace Umbraco.Community.MediaGovernance.Services.Settings;

public interface ISettingsService
{
	/// <summary>What is in force now: appsettings, with CMS overrides applied.</summary>
	Task<GovernanceOptions> GetAsync();

	/// <summary>The values from appsettings, before any CMS override.</summary>
	MediaGovernanceOptions AppSettings { get; }

	/// <summary>Cleans, validates and stores the CMS-editable settings; returns what is now in force.</summary>
	Task<GovernanceOptions> SaveAsync(SettingsUpdate update);

	/// <summary>
	/// Stores the admin overrides (only while <c>AllowAdminSettings</c> is on); returns what is now in force. A value equal to
	/// appsettings is stored as "follow appsettings", so it keeps following it if appsettings changes.
	/// </summary>
	Task<GovernanceOptions> SaveAdminAsync(AdminSettingsUpdate update);
}
