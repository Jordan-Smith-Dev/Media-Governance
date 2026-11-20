using System.Net;
using System.Text;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Jobs;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.Notifications;

/// <summary>Builds the HTML body of the daily "images need attention" email.</summary>
internal static class DigestBuilder
{
	/// <summary>Each group is capped so the first email on a large library stays readable.</summary>
	public const int MaxPerGroup = 50;

	private const string ImagePath = "/umbraco/section/media/workspace/media/edit/";
	private const string DashboardPath = "/umbraco/section/media/workspace/media-governance";

	private static readonly (GovernanceStatus Status, string Label)[] Order =
	[
		(GovernanceStatus.Expired, "Expired"),
		(GovernanceStatus.NoConsent, "No consent"),
		(GovernanceStatus.NeedsTagging, "Needs tagging"),
		(GovernanceStatus.ExpiringSoon, "Expiring soon"),
	];

	/// <summary>
	/// With a <paramref name="siteUrl"/>, each name links to that image's Governance tab and the email ends with a
	/// link to the dashboard. Without one (the site's address is not known yet) names are plain text.
	/// </summary>
	public static string Build(IReadOnlyList<Transition> transitions, IReadOnlyDictionary<Guid, string> names, Uri? siteUrl = null, IReadOnlyDictionary<Guid, string>? fileNames = null)
	{
		var body = new StringBuilder("<p>These images in the media library need attention:</p>");
		foreach (var (status, label) in Order)
		{
			var group = transitions.Where(t => t.To == status).ToList();
			if (group.Count == 0)
				continue;

			body.Append($"<h3>{label}</h3><ul>");
			foreach (var t in group.Take(MaxPerGroup))
			{
				var name = WebUtility.HtmlEncode(names.GetValueOrDefault(t.MediaKey, t.MediaKey.ToString()));
				// The file name (with extension) after the name makes the image easy to find in the library or on disk.
				var file = fileNames is not null && fileNames.TryGetValue(t.MediaKey, out var f) && !string.IsNullOrWhiteSpace(f)
					? $" ({WebUtility.HtmlEncode(f)})"
					: "";
				body.Append(siteUrl is null
					? $"<li>{name}{file}</li>"
					: $"<li><a href=\"{WebUtility.HtmlEncode(new Uri(siteUrl, ImagePath + t.MediaKey + "/invariant/view/governance").AbsoluteUri)}\">{name}</a>{file}</li>");
			}
			body.Append("</ul>");
			if (group.Count > MaxPerGroup)
				body.Append($"<p>and {group.Count - MaxPerGroup} more. See Governance for the full list.</p>");
		}
		if (siteUrl is not null)
			body.Append($"<p><a href=\"{WebUtility.HtmlEncode(new Uri(siteUrl, DashboardPath).AbsoluteUri)}\">Open Governance</a></p>");
		return body.ToString();
	}
}
