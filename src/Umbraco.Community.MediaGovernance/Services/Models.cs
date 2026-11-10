using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

public record PersonInput(string Name, string? Email, string? Phone, Guid? LinkedMemberKey, Guid? LinkedUserKey, string? JobTitle = null);

public record ConsentInput(
    Guid PersonId,
    DateOnly GivenOn,
    string GivenBy,
    IReadOnlyList<string> PermittedUses,
    DateOnly? ExpiresOn,
    string? Notes);

public record ImageGovernanceUpdate(
    DateOnly? DateTaken,
    DateOnly? DateObtained,
    DateOnly? CustomExpiry,
    bool NoPeople,
    IReadOnlyList<Guid> TaggedPersonIds);

public record GovernanceOptions(
    int RetentionYears,
    int WarningDays,
    IReadOnlyList<string> PermittedUses,
    IReadOnlyList<string> NotificationEmails)
{
    public GovernanceSettings ToCore() => new(RetentionYears, WarningDays);

    public static GovernanceOptions Default { get; } =
        new(3, 90, ["Web", "Print", "Social", "Marketing", "Other"], []);
}

public record ImageInfo(Guid Key, string Name, string Folder);

public record OverviewQuery(GovernanceStatus? Status, string? Filter, int Skip, int Take, IReadOnlyCollection<Guid>? Keys = null);

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
