using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.MediaGovernance.Middleware;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Who may use Governance: members of the configured user groups, or of Sensitive data when none are configured.</summary>
public class GovernanceAccessChecker(IOptions<MediaGovernanceOptions> options)
{
    public const string DefaultGroup = "sensitiveData";

    public static IReadOnlyList<string> GroupsFor(MediaGovernanceOptions o) =>
        o.AllowedUserGroups is { Length: > 0 } groups
            ? groups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).ToList()
            : [DefaultGroup];

    public IReadOnlyList<string> AllowedGroups => GroupsFor(options.Value);

    public bool IsAllowed(IUser? user) =>
        user is not null && user.Groups.Any(g => AllowedGroups.Contains(g.Alias, StringComparer.OrdinalIgnoreCase));
}
