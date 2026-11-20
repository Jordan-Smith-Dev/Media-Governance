using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

[TableName(TableNames.Notified)]
[PrimaryKey("MediaKey", AutoIncrement = false)]
[ExplicitColumns]
internal class NotifiedRow
{
	[Column("MediaKey")][PrimaryKeyColumn(AutoIncrement = false)] public Guid MediaKey { get; set; }
	[Column("LastStatus")] public int LastStatus { get; set; }
	[Column("NotifiedUtc")] public DateTime NotifiedUtc { get; set; }
}
