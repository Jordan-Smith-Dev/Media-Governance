namespace Umbraco.Community.MediaGovernance.Controllers.Models;

public record ImageGovernanceRequest(
	DateOnly? DateTaken,
	DateOnly? DateObtained,
	DateOnly? CustomExpiry,
	bool NoPeople,
	IReadOnlyList<Guid> TaggedPersonIds);
