using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Controllers;

public record PersonDto(Guid Id, string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle, int TaggedInCount, bool ConsentGiven = false, DateTime? ConsentUpdatedUtc = null);

public record PersonRequest(string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle = null);

/// <summary>Sets whether a person has given consent.</summary>
public record ConsentChangeRequest(bool Given);

/// <summary>A person tagged on an image, with whether they have given consent.</summary>
public record TaggedPersonDto(Guid PersonId, string Name, bool ConsentGiven);

/// <summary>An image a person is tagged in, for the person's page.</summary>
public record PersonImageDto(Guid MediaKey, string Name, string Folder, GovernanceStatus Status);

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
    IReadOnlyList<TaggedPersonDto> People,
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

/// <summary>The settings screen: what is in force, the appsettings defaults behind retention and the warning window, and the read-only values.</summary>
public record SettingsDto(
    int RetentionYears,
    int WarningDays,
    int DefaultRetentionYears,
    int DefaultWarningDays,
    IReadOnlyList<string> NotificationEmails,
    IReadOnlyList<Guid> NotificationUserGroupIds,
    bool BlockWithheldFiles,
    bool HideWithheldInPages,
    IReadOnlyList<string> AllowedUserGroups);
