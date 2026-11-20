namespace Umbraco.Community.MediaGovernance.Controllers.Models;

/// <summary>Sets whether a person has given consent, with whichever optional detail is switched on.</summary>
public record ConsentChangeRequest(bool Given, DateOnly? GivenOn = null, string? GivenBy = null, DateOnly? ExpiresOn = null, IReadOnlyList<string>? PermittedUses = null);
