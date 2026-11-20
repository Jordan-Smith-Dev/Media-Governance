namespace Umbraco.Community.MediaGovernance.Controllers.Models;

public record PersonRequest(string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle = null);
