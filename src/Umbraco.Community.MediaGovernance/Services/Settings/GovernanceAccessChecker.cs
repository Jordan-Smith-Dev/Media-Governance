using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.MediaGovernance.Middleware;

namespace Umbraco.Community.MediaGovernance.Services.Settings;

/// <summary>Who may use Governance: members of the configured user groups, or of Sensitive data when none are configured.</summary>
public class GovernanceAccessChecker
{
	private readonly IOptions<MediaGovernanceOptions> _options;

	public GovernanceAccessChecker(IOptions<MediaGovernanceOptions> options)
	{
		_options = options;
	}

	public const string DefaultGroup = "sensitiveData";

	public static IReadOnlyList<string> GroupsFor(MediaGovernanceOptions o) =>
		o.AllowedUserGroups is { Length: > 0 } groups
			? groups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).ToList()
			: [DefaultGroup];

	public const string DefaultAdminGroup = "admin";

	public static IReadOnlyList<string> AdminGroupsFor(MediaGovernanceOptions o) =>
		o.AdminSettingsUserGroups is { Length: > 0 } groups
			? groups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).ToList()
			: [DefaultAdminGroup];

	public IReadOnlyList<string> AllowedGroups => GroupsFor(_options.Value);

	/// <summary>True when the switch is on and the user may use Governance and is in an admin group.</summary>
	public bool IsAdmin(IUser? user) =>
		_options.Value.AllowAdminSettings && IsAllowed(user)
		&& user!.Groups.Any(g => AdminGroupsFor(_options.Value).Contains(g.Alias, StringComparer.OrdinalIgnoreCase));

	public bool IsAllowed(IUser? user) =>
		user is not null && user.Groups.Any(g => AllowedGroups.Contains(g.Alias, StringComparer.OrdinalIgnoreCase));
}
