using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

[TableName(TableNames.Image)]
[PrimaryKey("MediaKey", AutoIncrement = false)]
[ExplicitColumns]
public class ImageRow
{
	[Column("MediaKey")][PrimaryKeyColumn(AutoIncrement = false)] public Guid MediaKey { get; set; }
	[Column("DateTaken")][NullSetting(NullSetting = NullSettings.Null)] public DateTime? DateTaken { get; set; }
	[Column("DateObtained")][NullSetting(NullSetting = NullSettings.Null)] public DateTime? DateObtained { get; set; }
	[Column("CustomExpiry")][NullSetting(NullSetting = NullSettings.Null)] public DateTime? CustomExpiry { get; set; }
	[Column("NoPeople")] public bool NoPeople { get; set; }
}
