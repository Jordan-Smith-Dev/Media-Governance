namespace Umbraco.Community.MediaGovernance.Controllers.Models;

/// <summary>A person tagged on an image, with whether they have given consent.</summary>
internal record TaggedPersonDto(Guid PersonId, string Name, bool ConsentGiven, bool ConsentExpired = false);
