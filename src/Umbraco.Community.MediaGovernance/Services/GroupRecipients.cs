using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Services;

public record UserGroupInfo(Guid Id, string Name);

/// <summary>Looks up backoffice user groups and the email addresses of the people in them.</summary>
public interface IGroupRecipients
{
    Task<IReadOnlyList<UserGroupInfo>> ListGroupsAsync();

    /// <summary>Email addresses of approved, not-disabled users in the given groups. Unknown groups are skipped.</summary>
    Task<IReadOnlyList<string>> EmailsAsync(IEnumerable<Guid> groupIds);
}

public class GroupRecipients(IUserGroupService groups, IUserService users) : IGroupRecipients
{
    public async Task<IReadOnlyList<UserGroupInfo>> ListGroupsAsync() =>
        (await groups.GetAllAsync(0, 200)).Items
            .Select(g => new UserGroupInfo(g.Key, g.Name ?? g.Alias))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<IReadOnlyList<string>> EmailsAsync(IEnumerable<Guid> groupIds)
    {
        var emails = new List<string>();
        foreach (var id in groupIds)
        {
            var group = await groups.GetAsync(id);
            if (group is null) continue;
            emails.AddRange(users.GetAllInGroup(group.Id)
                .Where(u => u.IsApproved && !u.IsLockedOut && !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email));
        }
        return emails;
    }
}
