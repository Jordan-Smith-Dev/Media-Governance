using System.Net;
using System.Text;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Jobs;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Builds the HTML body of the daily "images need attention" email.</summary>
public static class DigestBuilder
{
    /// <summary>Each group is capped so the first email on a large library stays readable.</summary>
    public const int MaxPerGroup = 50;

    private static readonly (GovernanceStatus Status, string Label)[] Order =
    [
        (GovernanceStatus.Expired, "Expired"),
        (GovernanceStatus.NoConsent, "No consent"),
        (GovernanceStatus.NeedsTagging, "Needs tagging"),
        (GovernanceStatus.ExpiringSoon, "Expiring soon"),
    ];

    public static string Build(IReadOnlyList<Transition> transitions, IReadOnlyDictionary<Guid, string> names)
    {
        var body = new StringBuilder("<p>These images in the media library need attention:</p>");
        foreach (var (status, label) in Order)
        {
            var group = transitions.Where(t => t.To == status).ToList();
            if (group.Count == 0) continue;

            body.Append($"<h3>{label}</h3><ul>");
            foreach (var t in group.Take(MaxPerGroup))
                body.Append($"<li>{WebUtility.HtmlEncode(names.GetValueOrDefault(t.MediaKey, t.MediaKey.ToString()))}</li>");
            body.Append("</ul>");
            if (group.Count > MaxPerGroup)
                body.Append($"<p>and {group.Count - MaxPerGroup} more. See the Media governance dashboard for the full list.</p>");
        }
        return body.ToString();
    }
}
