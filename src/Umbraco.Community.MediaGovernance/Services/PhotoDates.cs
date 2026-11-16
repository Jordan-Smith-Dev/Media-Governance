using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>The date a photo says it was taken, according to its embedded camera data.</summary>
public interface IPhotoDates
{
    Task<DateOnly?> FindAsync(Guid mediaKey);
}

public class ExifPhotoDates(IMediaService media, MediaFileManager files) : IPhotoDates
{
    public Task<DateOnly?> FindAsync(Guid mediaKey)
    {
        var item = media.GetById(mediaKey);
        var src = MediaFilePath.ExtractSrc(item?.GetValue<string>("umbracoFile"));
        if (string.IsNullOrWhiteSpace(src)) return Task.FromResult<DateOnly?>(null);
        if (!src.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !src.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<DateOnly?>(null);

        try
        {
            var path = files.FileSystem.GetRelativePath(src);
            if (!files.FileSystem.FileExists(path)) return Task.FromResult<DateOnly?>(null);
            using var stream = files.FileSystem.OpenFile(path);
            return Task.FromResult(ExifDate.Read(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult<DateOnly?>(null);
        }
    }
}
