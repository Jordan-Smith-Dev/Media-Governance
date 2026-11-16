using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

public record PersonInput(string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle = null);

public record ImageGovernanceUpdate(
    DateOnly? DateTaken,
    DateOnly? DateObtained,
    DateOnly? CustomExpiry,
    bool NoPeople,
    IReadOnlyList<Guid> TaggedPersonIds);

/// <summary>One change applied to many images: mark them as having no people, set one expiry date, or clear it.</summary>
public record BulkImageChange(bool NoPeople, DateOnly? CustomExpiry, bool ClearCustomExpiry);

public record BulkImageResult(IReadOnlyList<Guid> Updated, IReadOnlyList<Guid> Skipped);

public record GovernanceOptions(
    int RetentionYears,
    int WarningDays,
    IReadOnlyList<string> PermittedUses,
    IReadOnlyList<string> NotificationEmails,
    bool? BlockWithheldFiles = null,
    bool? HideWithheldInPages = null,
    IReadOnlyList<Guid>? NotificationUserGroupIds = null,
    bool? ShowStatusSigns = null,
    bool? ColourTreeIcons = null,
    bool? ShowFolderMarkers = null)
{
    public GovernanceSettings ToCore() => new(RetentionYears, WarningDays);

    public static GovernanceOptions Default { get; } =
        new(3, 90, ["Web", "Print", "Social", "Marketing", "Other"], []);
}

public record ImageInfo(Guid Key, string Name, string Folder);

/// <summary>Columns the overview can be sorted by. Status sorts most urgent first.</summary>
public enum OverviewSort
{
    Name,
    Folder,
    DateTaken,
    Expiry,
    Status,
}

public record OverviewQuery(
    GovernanceStatus? Status,
    string? Filter,
    int Skip,
    int Take,
    IReadOnlyCollection<Guid>? Keys = null,
    OverviewSort Sort = OverviewSort.Name,
    bool Descending = false);

public record OverviewItem(
    Guid MediaKey,
    string Name,
    string Folder,
    IReadOnlyList<string> People,
    DateOnly? DateTaken,
    DateOnly? Expiry,
    int? DaysToExpiry,
    string Consent,
    GovernanceStatus Status,
    ExpirySource ExpirySource = ExpirySource.None,
    IReadOnlyList<GovernanceStatus>? Issues = null,
    bool NoPeople = false);

public record OverviewPage(
    IReadOnlyDictionary<GovernanceStatus, int> Counts,
    int AllCount,
    IReadOnlyList<OverviewItem> Items,
    int Total);

/// <summary>Who gave a consent. A fixed list: free text could record a consent that is not valid.</summary>
public static class ConsentGivenBy
{
    public const string Self = "Self";
    public const string ParentOrGuardian = "ParentOrGuardian";
    public const string LegalRepresentative = "LegalRepresentative";

    public static readonly IReadOnlyList<string> All = [Self, ParentOrGuardian, LegalRepresentative];
}

/// <summary>Actor name used for audit entries written by background work.</summary>
public static class Actors
{
    public const string System = "system";
}
