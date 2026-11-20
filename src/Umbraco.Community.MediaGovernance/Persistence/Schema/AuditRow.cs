using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

[TableName(TableNames.Audit)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class AuditRow
{
	[Column("Id")][PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
	[Column("WhenUtc")] public DateTime WhenUtc { get; set; }
	[Column("ActorKey")][Length(64)] public string ActorKey { get; set; } = string.Empty;
	[Column("Action")][Length(64)] public string Action { get; set; } = string.Empty;
	[Column("MediaKey")][NullSetting(NullSetting = NullSettings.Null)] public Guid? MediaKey { get; set; }
	[Column("PersonId")][NullSetting(NullSetting = NullSettings.Null)] public Guid? PersonId { get; set; }
	[Column("Detail")][NullSetting(NullSetting = NullSettings.Null)][Length(1000)] public string? Detail { get; set; }
}
