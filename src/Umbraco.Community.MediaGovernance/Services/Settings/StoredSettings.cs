namespace Umbraco.Community.MediaGovernance.Services.Settings;

/// <summary>What is stored: only values that differ from appsettings, so the rest keeps following appsettings.</summary>
internal record StoredSettings(
	int? RetentionYears,
	int? WarningDays,
	IReadOnlyList<string>? NotificationEmails,
	IReadOnlyList<Guid>? NotificationUserGroupIds,
	AdminOverrides? Admin = null);
