namespace Umbraco.Community.MediaGovernance.Core.Models;

public record ImageGovernanceFacts(
    Guid MediaKey,
    DateOnly? DateTaken,
    DateOnly? DateObtained,
    DateOnly? CustomExpiry,
    bool NoPeople,
    IReadOnlyList<Guid> TaggedPersonIds,
    IReadOnlyList<ConsentFacts> Consents,
    DateOnly? DateUploaded = null);
