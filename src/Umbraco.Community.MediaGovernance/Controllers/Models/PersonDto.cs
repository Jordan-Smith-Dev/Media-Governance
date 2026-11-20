namespace Umbraco.Community.MediaGovernance.Controllers.Models;

internal record PersonDto(
	Guid Id, string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle, int TaggedInCount,
	bool ConsentGiven = false, DateTime? ConsentUpdatedUtc = null,
	// Consent is "in force" when it has been given and, if expiry is on, has not run out. Pills and tags use this.
	bool ConsentInForce = false, bool ConsentExpired = false,
	// Optional detail, each null unless its option is switched on.
	DateOnly? ConsentGivenOn = null, string? ConsentGivenBy = null, DateOnly? ConsentExpiresOn = null,
	IReadOnlyList<string>? ConsentPermittedUses = null,
	bool HasEvidence = false, string? EvidenceType = null, long? EvidenceSizeBytes = null);
