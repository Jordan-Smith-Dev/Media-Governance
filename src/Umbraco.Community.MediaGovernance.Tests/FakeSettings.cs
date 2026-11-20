using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeSettings : ISettingsService
{
	public GovernanceOptions Options { get; set; } = new(3, 90, []);
	public Middleware.MediaGovernanceOptions AppSettings { get; set; } = new();
	public Task<GovernanceOptions> GetAsync() => Task.FromResult(Options);
	public Task<GovernanceOptions> SaveAdminAsync(AdminSettingsUpdate update)
	{
		Options = Options with
		{
			BlockWithheldFiles = update.BlockWithheldFiles,
			HideWithheldInPages = update.HideWithheldInPages,
			ShowStatusSigns = update.ShowStatusSigns,
			ColourTreeIcons = update.ColourTreeIcons,
			ShowFolderMarkers = update.ShowFolderMarkers,
			ConsentDetails = update.ConsentDetails,
			ConsentExpiry = update.ConsentExpiry,
			ConsentEvidence = update.ConsentEvidence,
			PermittedUses = update.PermittedUses
		};
		return Task.FromResult(Options);
	}
	public Task<GovernanceOptions> SaveAsync(SettingsUpdate update)
	{
		Options = Options with { RetentionYears = update.RetentionYears, WarningDays = update.WarningDays, NotificationEmails = update.NotificationEmails, NotificationUserGroupIds = update.NotificationUserGroupIds };
		return Task.FromResult(Options);
	}
}
