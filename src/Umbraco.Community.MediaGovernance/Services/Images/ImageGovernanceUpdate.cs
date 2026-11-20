namespace Umbraco.Community.MediaGovernance.Services.Images;

public record ImageGovernanceUpdate(
	DateOnly? DateTaken,
	DateOnly? DateObtained,
	DateOnly? CustomExpiry,
	bool NoPeople,
	IReadOnlyList<Guid> TaggedPersonIds);
