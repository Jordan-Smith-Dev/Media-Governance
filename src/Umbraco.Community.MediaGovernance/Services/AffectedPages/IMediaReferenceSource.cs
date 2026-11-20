namespace Umbraco.Community.MediaGovernance.Services.AffectedPages;

/// <summary>Finds the pages (documents) that use a media item.</summary>
public interface IMediaReferenceSource
{
	Task<IReadOnlyList<MediaReferrer>> GetPagesUsingAsync(Guid mediaKey);
}
