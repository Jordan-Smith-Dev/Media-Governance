namespace Umbraco.Community.MediaGovernance.Services.Images;

/// <summary>When each media item was uploaded (created). Used when an image has no date taken or obtained.</summary>
public interface IMediaUploadDates
{
	Task<IReadOnlyDictionary<Guid, DateOnly>> GetAsync(IReadOnlyCollection<Guid> mediaKeys);
}
