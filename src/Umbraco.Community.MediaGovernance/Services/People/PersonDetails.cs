namespace Umbraco.Community.MediaGovernance.Services.People;

public record PersonDetails(Guid Id, string Name, string? Email, string? Phone, string? JobTitle, Guid? LinkedMemberKey, Guid? LinkedUserKey, DateTime CreatedUtc);
