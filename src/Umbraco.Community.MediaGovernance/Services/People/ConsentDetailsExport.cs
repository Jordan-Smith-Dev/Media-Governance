namespace Umbraco.Community.MediaGovernance.Services.People;

public record ConsentDetailsExport(bool Given, DateTime? LastChangedUtc, DateOnly? GivenOn, string? GivenBy, DateOnly? ExpiresOn, IReadOnlyList<string> PermittedUses, string? EvidenceFile);
