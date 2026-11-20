using Umbraco.Community.MediaGovernance.Services.Images;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeCatalog : IImageCatalog
{
	private readonly ImageInfo[] _images;

	public FakeCatalog(params ImageInfo[] images)
	{
		_images = images;
	}

	public Task<IReadOnlyList<ImageInfo>> ListImagesAsync() => Task.FromResult<IReadOnlyList<ImageInfo>>(_images);
}
