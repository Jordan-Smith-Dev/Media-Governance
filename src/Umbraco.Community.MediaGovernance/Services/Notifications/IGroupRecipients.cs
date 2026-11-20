namespace Umbraco.Community.MediaGovernance.Services.Notifications;

/// <summary>Looks up backoffice user groups and the email addresses of the people in them.</summary>
public interface IGroupRecipients
{
	Task<IReadOnlyList<UserGroupInfo>> ListGroupsAsync();

	/// <summary>Email addresses of approved, not-disabled users in the given groups. Unknown groups are skipped.</summary>
	Task<IReadOnlyList<string>> EmailsAsync(IEnumerable<Guid> groupIds);
}
