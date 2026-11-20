namespace Umbraco.Community.MediaGovernance.Services.Images;

/// <summary>Lists the image media items the dashboard governs.</summary>
public interface IImageCatalog
{
	Task<IReadOnlyList<ImageInfo>> ListImagesAsync();
}
