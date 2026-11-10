namespace Umbraco.Community.MediaGovernance.Core.Models;

public record ConsentFacts(
    Guid PersonId,
    DateOnly GivenOn,
    DateOnly? ExpiresOn,
    DateOnly? WithdrawnOn,
    IReadOnlyList<string>? PermittedUses = null);
