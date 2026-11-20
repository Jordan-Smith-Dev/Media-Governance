namespace Umbraco.Community.MediaGovernance.Controllers.Models;

/// <summary>What is in force, and what appsettings says, so the screen can show "overridden" and offer a reset.</summary>
internal record AdminSettingsDto(AdminValues Current, AdminValues Defaults);
