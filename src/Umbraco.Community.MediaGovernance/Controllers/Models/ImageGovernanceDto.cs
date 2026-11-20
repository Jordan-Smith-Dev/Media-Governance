using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Controllers.Models;

internal record ImageGovernanceDto(
	Guid MediaKey,
	DateOnly? DateTaken,
	DateOnly? DateObtained,
	DateOnly? CustomExpiry,
	bool NoPeople,
	IReadOnlyList<Guid> TaggedPersonIds,
	DateOnly? EffectiveExpiry,
	DateOnly? DefaultExpiry,
	ExpirySource ExpirySource,
	GovernanceStatus Status,
	IReadOnlyList<GovernanceStatus> Issues,
	IReadOnlyList<TaggedPersonDto> People,
	IReadOnlyList<string> ClearedUses,
	DateOnly? DateUploaded = null,
	DateOnly? ExifDateTaken = null);
