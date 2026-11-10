using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

/// <summary>The normalised URL path of a governed image's file, so a file request can be matched to its media item.</summary>
[TableName(TableNames.MediaFile)]
[PrimaryKey("MediaKey", AutoIncrement = false)]
[ExplicitColumns]
public class MediaFileRow
{
    [Column("MediaKey")] [PrimaryKeyColumn(AutoIncrement = false)] public Guid MediaKey { get; set; }
    [Column("FilePath")] [Length(500)] [Index(IndexTypes.NonClustered, Name = "IX_mgMediaFile_FilePath")] public string FilePath { get; set; } = string.Empty;
}
