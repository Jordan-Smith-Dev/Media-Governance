namespace Umbraco.Community.MediaGovernance.Services.AffectedPages;

public record MediaReferrer(Guid PageKey, string PageName, bool Published, string? ContentTypeName, string? ContentTypeIcon);
