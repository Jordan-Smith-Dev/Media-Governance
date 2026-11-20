namespace Umbraco.Community.MediaGovernance.Services.Images;

public record BulkImageResult(IReadOnlyList<Guid> Updated, IReadOnlyList<Guid> Skipped);
