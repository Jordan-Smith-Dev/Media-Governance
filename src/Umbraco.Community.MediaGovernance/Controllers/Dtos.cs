using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Controllers;

public record PersonDto(Guid Id, string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle, int TaggedInCount);

public record PersonRequest(string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle = null);

public record ConsentDto(
    Guid Id,
    Guid PersonId,
    DateOnly GivenOn,
    string GivenBy,
    IReadOnlyList<string> PermittedUses,
    DateOnly? ExpiresOn,
    DateOnly? WithdrawnOn,
    bool HasEvidence,
    string? Notes,
    string? EvidenceType = null,
    long? EvidenceSizeBytes = null);

public record EvidenceEventDto(string Action, DateTime WhenUtc, string Actor);

public record ConsentRequest(
    Guid PersonId,
    DateOnly GivenOn,
    string GivenBy,
    IReadOnlyList<string> PermittedUses,
    DateOnly? ExpiresOn,
    string? Notes);

public record WithdrawRequest(DateOnly? On);

public record ImageGovernanceDto(
    Guid MediaKey,
    DateOnly? DateTaken,
    DateOnly? DateObtained,
    DateOnly? CustomExpiry,
    bool NoPeople,
    IReadOnlyList<Guid> TaggedPersonIds,
    DateOnly? EffectiveExpiry,
    ExpirySource ExpirySource,
    GovernanceStatus Status,
    IReadOnlyList<GovernanceStatus> Issues,
    IReadOnlyList<string> ClearedUses,
    IReadOnlyList<ConsentDto> Consents,
    DateOnly? DateUploaded = null,
    DateOnly? ExifDateTaken = null);

public record BulkImageRequest(IReadOnlyList<Guid> MediaKeys, bool NoPeople, DateOnly? CustomExpiry, bool ClearCustomExpiry);

public record BulkImageDto(int Updated, int Skipped);

public record ImageGovernanceRequest(
    DateOnly? DateTaken,
    DateOnly? DateObtained,
    DateOnly? CustomExpiry,
    bool NoPeople,
    IReadOnlyList<Guid> TaggedPersonIds);
