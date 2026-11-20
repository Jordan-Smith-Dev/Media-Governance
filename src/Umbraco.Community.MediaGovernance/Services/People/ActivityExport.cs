namespace Umbraco.Community.MediaGovernance.Services.People;

/// <summary>One audit entry about this person: what happened, when, and who did it (a user name where it can be found).</summary>
public record ActivityExport(DateTime WhenUtc, string Action, string By);
