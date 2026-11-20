using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;
using Umbraco.Community.MediaGovernance.TagHelpers;

namespace Umbraco.Community.MediaGovernance.Tests;

public class MediaGovernanceTagHelperTests
{
	private class FakeUsage : IMediaGovernanceUsage
	{
		private readonly GovernanceStatus _status;

		public FakeUsage(GovernanceStatus status)
		{
			_status = status;
		}

		public Task<GovernanceStatus> GetStatusAsync(Guid mediaKey) => Task.FromResult(_status);
		public Task<bool> IsUsableAsync(Guid mediaKey) => Task.FromResult(UsagePolicy.IsUsable(_status));
		public Task<IReadOnlyDictionary<Guid, bool>> GetUsableAsync(IReadOnlyCollection<Guid> mediaKeys) => throw new NotSupportedException();
	}

	private class FakeReporter : IWithheldMediaReporter
	{
		public List<(Guid Key, GovernanceStatus Status, string? Path)> Reports { get; } = new();
		public void Report(Guid mediaKey, GovernanceStatus status, string? pagePath) => Reports.Add((mediaKey, status, pagePath));
	}

	private class FakeHttpContextAccessor : IHttpContextAccessor
	{
		public FakeHttpContextAccessor(string? path)
		{
			HttpContext = path is null ? null : new DefaultHttpContext { Request = { Path = path } };
		}

		public HttpContext? HttpContext { get; set; }
	}

	private static async Task<TagHelperOutput> Run(MediaGovernanceTagHelper helper)
	{
		var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
		var output = new TagHelperOutput("media-governance", new TagHelperAttributeList(), (_, _) =>
		{
			var content = new DefaultTagHelperContent();
			content.SetHtmlContent("<img>");
			return Task.FromResult<TagHelperContent>(content);
		});
		await helper.ProcessAsync(context, output);
		return output;
	}

	private class FakePolicy : IFrontEndPolicyProvider
	{
		private readonly bool _hidePages;

		public FakePolicy(bool hidePages = true)
		{
			_hidePages = hidePages;
		}

		public Task<FrontEndPolicy> GetAsync() => Task.FromResult(new FrontEndPolicy(true, _hidePages));
	}

	private readonly FakeReporter _reporter = new();

	private MediaGovernanceTagHelper Helper(GovernanceStatus status, UsageDisplay show = UsageDisplay.Usable, string? path = "/news/article", bool hidePages = true) =>
		new(new FakeUsage(status), _reporter, new FakeHttpContextAccessor(path), new FakePolicy(hidePages)) { Key = Guid.NewGuid(), Show = show };

	[Fact]
	public async Task When_the_site_chooses_not_to_hide_withheld_images_they_still_show_but_are_logged()
	{
		var output = await Run(Helper(GovernanceStatus.Expired, hidePages: false));
		Assert.False(output.IsContentModified);
		Assert.Single(_reporter.Reports);
	}

	[Fact]
	public async Task When_not_hiding_the_fallback_is_not_shown_either()
	{
		var output = await Run(Helper(GovernanceStatus.Expired, UsageDisplay.Unusable, hidePages: false));
		Assert.True(output.Content.IsEmptyOrWhiteSpace);
		Assert.True(output.IsContentModified);
	}

	[Fact]
	public async Task Usable_image_renders_its_content_without_a_wrapper_element()
	{
		var output = await Run(Helper(GovernanceStatus.Compliant));
		Assert.Null(output.TagName);
		Assert.False(output.IsContentModified);
		Assert.Equal("<img>", (await output.GetChildContentAsync()).GetContent());
		Assert.Empty(_reporter.Reports);
	}

	[Fact]
	public async Task Unusable_image_renders_nothing()
	{
		var output = await Run(Helper(GovernanceStatus.Expired));
		Assert.Null(output.TagName);
		Assert.True(output.Content.IsEmptyOrWhiteSpace);
		Assert.True(output.IsContentModified);
	}

	[Fact]
	public async Task Show_Unusable_renders_the_fallback_only_when_the_image_is_unusable()
	{
		var shown = await Run(Helper(GovernanceStatus.NoConsent, UsageDisplay.Unusable));
		Assert.False(shown.IsContentModified);

		var hidden = await Run(Helper(GovernanceStatus.Compliant, UsageDisplay.Unusable));
		Assert.True(hidden.Content.IsEmptyOrWhiteSpace);
		Assert.True(hidden.IsContentModified);
	}

	[Theory]
	[InlineData(GovernanceStatus.Expired)]
	[InlineData(GovernanceStatus.NoConsent)]
	public async Task Withheld_image_is_reported_with_its_status_and_the_page_path(GovernanceStatus status)
	{
		var helper = Helper(status, path: "/news/article");
		await Run(helper);
		var report = Assert.Single(_reporter.Reports);
		Assert.Equal(helper.Key, report.Key);
		Assert.Equal(status, report.Status);
		Assert.Equal("/news/article", report.Path);
	}

	[Fact]
	public async Task Withheld_image_is_reported_once_even_when_a_fallback_is_also_rendered()
	{
		await Run(Helper(GovernanceStatus.Expired, UsageDisplay.Unusable));
		Assert.Single(_reporter.Reports);
	}

	[Fact]
	public async Task Missing_request_context_still_reports_without_a_path()
	{
		await Run(Helper(GovernanceStatus.Expired, path: null));
		Assert.Null(Assert.Single(_reporter.Reports).Path);
	}

	[Fact]
	public async Task Missing_key_is_a_developer_error_not_a_silent_pass() =>
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			Run(new MediaGovernanceTagHelper(new FakeUsage(GovernanceStatus.Compliant), _reporter, new FakeHttpContextAccessor("/"), new FakePolicy())));
}
