using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

// Dates are stored as DateTime (midnight) because NPoco has no DateOnly mapping; repositories convert.
[TableName(TableNames.Person)]
[PrimaryKey("Id", AutoIncrement = false)]
[ExplicitColumns]
public class PersonRow
{
	[Column("Id")][PrimaryKeyColumn(AutoIncrement = false)] public Guid Id { get; set; }
	[Column("Name")][Length(255)] public string Name { get; set; } = string.Empty;
	[Column("Email")][NullSetting(NullSetting = NullSettings.Null)][Length(255)] public string? Email { get; set; }
	[Column("Phone")][NullSetting(NullSetting = NullSettings.Null)][Length(50)] public string? Phone { get; set; }
	[Column("JobTitle")][NullSetting(NullSetting = NullSettings.Null)][Length(100)] public string? JobTitle { get; set; }
	[Column("LinkedMemberKey")][NullSetting(NullSetting = NullSettings.Null)] public Guid? LinkedMemberKey { get; set; }
	[Column("LinkedUserKey")][NullSetting(NullSetting = NullSettings.Null)] public Guid? LinkedUserKey { get; set; }
	[Column("ConsentGiven")] public bool ConsentGiven { get; set; }
	[Column("ConsentUpdatedUtc")][NullSetting(NullSetting = NullSettings.Null)] public DateTime? ConsentUpdatedUtc { get; set; }
	// Optional consent detail (see the ConsentDetails, ConsentExpiry, PermittedUses and ConsentEvidence options).
	[Column("ConsentGivenOn")][NullSetting(NullSetting = NullSettings.Null)] public DateTime? ConsentGivenOn { get; set; }
	[Column("ConsentGivenBy")][NullSetting(NullSetting = NullSettings.Null)][Length(30)] public string? ConsentGivenBy { get; set; }
	[Column("ConsentExpiresOn")][NullSetting(NullSetting = NullSettings.Null)] public DateTime? ConsentExpiresOn { get; set; }
	[Column("ConsentPermittedUses")][NullSetting(NullSetting = NullSettings.Null)][Length(500)] public string? ConsentPermittedUses { get; set; }
	[Column("ConsentEvidencePath")][NullSetting(NullSetting = NullSettings.Null)][Length(500)] public string? ConsentEvidencePath { get; set; }
	[Column("CreatedUtc")] public DateTime CreatedUtc { get; set; }
}
