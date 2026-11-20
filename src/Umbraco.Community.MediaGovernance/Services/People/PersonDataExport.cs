namespace Umbraco.Community.MediaGovernance.Services.People;

/// <summary>Everything held about one person, for a subject access request (GDPR Art. 15).</summary>
public record PersonDataExport(
	DateTime ExportedUtc,
	PersonDetails Person,
	ConsentDetailsExport Consent,
	IReadOnlyList<TaggedImageExport> TaggedImages,
	IReadOnlyList<ActivityExport> Activity);
