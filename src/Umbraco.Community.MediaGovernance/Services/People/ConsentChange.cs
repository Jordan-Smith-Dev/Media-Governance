namespace Umbraco.Community.MediaGovernance.Services.People;

/// <summary>
/// A change to a person's consent. Only the parts whose option is switched on are used: given-on and given-by with
/// ConsentDetails, the end date with ConsentExpiry, and the uses when permitted uses are configured.
/// </summary>
public record ConsentChange(bool Given, DateOnly? GivenOn = null, string? GivenBy = null, DateOnly? ExpiresOn = null, IReadOnlyList<string>? PermittedUses = null);
