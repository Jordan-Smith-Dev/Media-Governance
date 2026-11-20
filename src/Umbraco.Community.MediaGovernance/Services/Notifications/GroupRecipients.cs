using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Services.Notifications;

internal class GroupRecipients : IGroupRecipients
{
	private readonly IUserGroupService _groups;
	private readonly IUserService _users;

	public GroupRecipients(IUserGroupService groups, IUserService users)
	{
		_groups = groups;
		_users = users;
	}

	public async Task<IReadOnlyList<UserGroupInfo>> ListGroupsAsync() =>
		(await _groups.GetAllAsync(0, 200)).Items
			.Select(g => new UserGroupInfo(g.Key, g.Name ?? g.Alias))
			.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

	public async Task<IReadOnlyList<string>> EmailsAsync(IEnumerable<Guid> groupIds)
	{
		var emails = new List<string>();
		foreach (var id in groupIds)
		{
			var group = await _groups.GetAsync(id);
			if (group is null)
				continue;
			emails.AddRange(_users.GetAllInGroup(group.Id)
				.Where(u => u.IsApproved && !u.IsLockedOut && !string.IsNullOrWhiteSpace(u.Email))
				.Select(u => u.Email));
		}
		return emails;
	}
}
