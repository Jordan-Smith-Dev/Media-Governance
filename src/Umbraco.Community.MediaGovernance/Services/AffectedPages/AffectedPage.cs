namespace Umbraco.Community.MediaGovernance.Services.AffectedPages;

public record AffectedPage(Guid PageKey, string Name, bool Published, string? ContentType, string? ContentTypeIcon, IReadOnlyList<AffectedImage> Images);
