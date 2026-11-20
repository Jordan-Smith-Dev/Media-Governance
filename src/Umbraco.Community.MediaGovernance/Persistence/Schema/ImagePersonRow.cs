using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

[TableName(TableNames.ImagePerson)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal class ImagePersonRow
{
	[Column("Id")][PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
	[Column("MediaKey")][Index(IndexTypes.NonClustered, Name = "IX_mgImagePerson_MediaKey")] public Guid MediaKey { get; set; }
	[Column("PersonId")][Index(IndexTypes.NonClustered, Name = "IX_mgImagePerson_PersonId")] public Guid PersonId { get; set; }
}
