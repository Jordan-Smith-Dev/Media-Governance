namespace Umbraco.Community.MediaGovernance.Services.Settings;

/// <summary>The only settings the CMS can change.</summary>
public record SettingsUpdate(
	int RetentionYears,
	int WarningDays,
	IReadOnlyList<string> NotificationEmails,
	IReadOnlyList<Guid>? NotificationUserGroupIds = null);
