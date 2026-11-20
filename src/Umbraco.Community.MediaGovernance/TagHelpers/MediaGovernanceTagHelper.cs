using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Services;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;

namespace Umbraco.Community.MediaGovernance.TagHelpers;

/// <summary>
/// Wraps markup that shows a media item and drops it when the image has expired or lacks consent.
/// <code>
/// &lt;media-governance media="@Model.Image"&gt;&lt;img src="@Model.Image.Url()" /&gt;&lt;/media-governance&gt;
/// &lt;media-governance media="@Model.Image" show="Unusable"&gt;&lt;p&gt;Image no longer available&lt;/p&gt;&lt;/media-governance&gt;
/// </code>
/// Enable it with <c>@addTagHelper *, Umbraco.Community.MediaGovernance</c> in <c>_ViewImports.cshtml</c>.
/// No wrapper element is written to the page. Each time an image is withheld, a warning naming the page is
/// written to the Umbraco log (de-duplicated), so editors can find and fix pages that still use it.
/// </summary>
[HtmlTargetElement("media-governance")]
public class MediaGovernanceTagHelper : TagHelper
{
	private readonly IMediaGovernanceUsage _usage;
	private readonly IWithheldMediaReporter _reporter;
	private readonly IHttpContextAccessor _httpContext;
	private readonly IFrontEndPolicyProvider _policy;

	public MediaGovernanceTagHelper(
		IMediaGovernanceUsage usage,
		IWithheldMediaReporter reporter,
		IHttpContextAccessor httpContext,
		IFrontEndPolicyProvider policy)
	{
		_usage = usage;
		_reporter = reporter;
		_httpContext = httpContext;
		_policy = policy;
	}

	/// <summary>The media item (use this or <see cref="Key"/>).</summary>
	[HtmlAttributeName("media")]
	public IPublishedContent? Media { get; set; }

	/// <summary>The media item's key (use this or <see cref="Media"/>).</summary>
	[HtmlAttributeName("key")]
	public Guid? Key { get; set; }

	[HtmlAttributeName("show")]
	public UsageDisplay Show { get; set; } = UsageDisplay.Usable;

	public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
	{
		var key = Key ?? Media?.Key
			?? throw new InvalidOperationException("<media-governance> needs a 'media' or 'key' attribute.");

		output.TagName = null; // no wrapper element in the page

		var status = await _usage.GetStatusAsync(key);
		var withheld = !UsagePolicy.IsUsable(status);
		if (withheld)
			_reporter.Report(key, status, _httpContext.HttpContext?.Request.Path.Value);

		// When the site has chosen not to hide withheld images, they show as normal (and are still logged above).
		var usable = !withheld || !(await _policy.GetAsync()).HidePages;
		if ((Show == UsageDisplay.Usable) != usable)
			output.SuppressOutput();
	}
}
