using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

public static class TableNames
{
    public const string Person = "mgPerson";
    public const string Image = "mgImage";
    public const string ImagePerson = "mgImagePerson";
    public const string Consent = "mgConsent";
    public const string Audit = "mgAudit";
    public const string Notified = "mgNotified";
    public const string MediaFile = "mgMediaFile";
}

// Dates are stored as DateTime (midnight) because NPoco has no DateOnly mapping; repositories convert.

[TableName(TableNames.Person)]
[PrimaryKey("Id", AutoIncrement = false)]
[ExplicitColumns]
public class PersonRow
{
    [Column("Id")] [PrimaryKeyColumn(AutoIncrement = false)] public Guid Id { get; set; }
    [Column("Name")] [Length(255)] public string Name { get; set; } = string.Empty;
    [Column("Email")] [NullSetting(NullSetting = NullSettings.Null)] [Length(255)] public string? Email { get; set; }
    [Column("Phone")] [NullSetting(NullSetting = NullSettings.Null)] [Length(50)] public string? Phone { get; set; }
    [Column("JobTitle")] [NullSetting(NullSetting = NullSettings.Null)] [Length(100)] public string? JobTitle { get; set; }
    [Column("LinkedMemberKey")] [NullSetting(NullSetting = NullSettings.Null)] public Guid? LinkedMemberKey { get; set; }
    [Column("LinkedUserKey")] [NullSetting(NullSetting = NullSettings.Null)] public Guid? LinkedUserKey { get; set; }
    [Column("CreatedUtc")] public DateTime CreatedUtc { get; set; }
}

[TableName(TableNames.Image)]
[PrimaryKey("MediaKey", AutoIncrement = false)]
[ExplicitColumns]
public class ImageRow
{
    [Column("MediaKey")] [PrimaryKeyColumn(AutoIncrement = false)] public Guid MediaKey { get; set; }
    [Column("DateTaken")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? DateTaken { get; set; }
    [Column("DateObtained")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? DateObtained { get; set; }
    [Column("CustomExpiry")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? CustomExpiry { get; set; }
    [Column("NoPeople")] public bool NoPeople { get; set; }
}

[TableName(TableNames.ImagePerson)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class ImagePersonRow
{
    [Column("Id")] [PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
    [Column("MediaKey")] [Index(IndexTypes.NonClustered, Name = "IX_mgImagePerson_MediaKey")] public Guid MediaKey { get; set; }
    [Column("PersonId")] [Index(IndexTypes.NonClustered, Name = "IX_mgImagePerson_PersonId")] public Guid PersonId { get; set; }
}

[TableName(TableNames.Consent)]
[PrimaryKey("Id", AutoIncrement = false)]
[ExplicitColumns]
public class ConsentRow
{
    [Column("Id")] [PrimaryKeyColumn(AutoIncrement = false)] public Guid Id { get; set; }
    [Column("MediaKey")] [Index(IndexTypes.NonClustered, Name = "IX_mgConsent_MediaKey")] public Guid MediaKey { get; set; }
    [Column("PersonId")] [Index(IndexTypes.NonClustered, Name = "IX_mgConsent_PersonId")] public Guid PersonId { get; set; }
    [Column("GivenOn")] public DateTime GivenOn { get; set; }
    [Column("GivenBy")] [Length(255)] public string GivenBy { get; set; } = string.Empty;
    [Column("PermittedUses")] [Length(500)] public string PermittedUses { get; set; } = string.Empty;
    [Column("ExpiresOn")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? ExpiresOn { get; set; }
    [Column("WithdrawnOn")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? WithdrawnOn { get; set; }
    [Column("EvidencePath")] [NullSetting(NullSetting = NullSettings.Null)] [Length(500)] public string? EvidencePath { get; set; }
    [Column("Notes")] [NullSetting(NullSetting = NullSettings.Null)] [SpecialDbType(SpecialDbTypes.NVARCHARMAX)] public string? Notes { get; set; }
}

[TableName(TableNames.Audit)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class AuditRow
{
    [Column("Id")] [PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
    [Column("WhenUtc")] public DateTime WhenUtc { get; set; }
    [Column("ActorKey")] [Length(64)] public string ActorKey { get; set; } = string.Empty;
    [Column("Action")] [Length(64)] public string Action { get; set; } = string.Empty;
    [Column("MediaKey")] [NullSetting(NullSetting = NullSettings.Null)] public Guid? MediaKey { get; set; }
    [Column("PersonId")] [NullSetting(NullSetting = NullSettings.Null)] public Guid? PersonId { get; set; }
    [Column("Detail")] [NullSetting(NullSetting = NullSettings.Null)] [Length(1000)] public string? Detail { get; set; }
}

[TableName(TableNames.Notified)]
[PrimaryKey("MediaKey", AutoIncrement = false)]
[ExplicitColumns]
public class NotifiedRow
{
    [Column("MediaKey")] [PrimaryKeyColumn(AutoIncrement = false)] public Guid MediaKey { get; set; }
    [Column("LastStatus")] public int LastStatus { get; set; }
    [Column("NotifiedUtc")] public DateTime NotifiedUtc { get; set; }
}
