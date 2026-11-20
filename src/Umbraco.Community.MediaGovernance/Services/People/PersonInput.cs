namespace Umbraco.Community.MediaGovernance.Services.People;

public record PersonInput(string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle = null);
