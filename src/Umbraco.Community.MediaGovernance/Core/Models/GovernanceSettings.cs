namespace Umbraco.Community.MediaGovernance.Core.Models;

public record GovernanceSettings(int RetentionYears = 3, int WarningDays = 90);
