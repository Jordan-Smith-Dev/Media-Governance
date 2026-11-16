namespace Umbraco.Community.MediaGovernance.Core.Models;

public record ConsentFacts(
    Guid PersonId,
    DateOnly GivenOn,
    DateOnly? ExpiresOn,
    DateOnly? WithdrawnOn,
    IReadOnlyList<string>? PermittedUses = null)
{
    /// <summary>Stand-in "given on" date for a plain yes/no consent, which has no date of its own.</summary>
    public static readonly DateOnly Always = new(2000, 1, 1);
}
