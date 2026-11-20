namespace Umbraco.Community.MediaGovernance.Persistence.Schema;

internal static class TableNames
{
	public const string Person = "mgPerson";
	public const string Image = "mgImage";
	public const string ImagePerson = "mgImagePerson";
	/// <summary>Retired: consent used to be stored per image and person. Kept only so the migration can drop it.</summary>
	public const string LegacyConsent = "mgConsent";
	public const string Audit = "mgAudit";
	public const string Notified = "mgNotified";
	public const string MediaFile = "mgMediaFile";
}
