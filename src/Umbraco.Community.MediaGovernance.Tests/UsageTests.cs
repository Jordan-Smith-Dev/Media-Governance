using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services;
using Umbraco.Community.MediaGovernance.TagHelpers;

namespace Umbraco.Community.MediaGovernance.Tests;

public class UsagePolicyTests
{
    [Theory]
    [InlineData(GovernanceStatus.Compliant, true)]
    [InlineData(GovernanceStatus.ExpiringSoon, true)]
    [InlineData(GovernanceStatus.NeedsTagging, true)]
    [InlineData(GovernanceStatus.Expired, false)]
    [InlineData(GovernanceStatus.NoConsent, false)]
    public void Only_expired_and_no_consent_images_are_unusable(GovernanceStatus status, bool usable) =>
        Assert.Equal(usable, UsagePolicy.IsUsable(status));
}

public class MediaGovernanceUsageTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);
    private readonly FakeImageRepository _images = new();

    private MediaGovernanceUsage Sut() =>
        new(new StatusLookupService(_images, new FakeSettings(), new FixedTimeProvider(Today)));

    private Guid Add(DateOnly? taken, bool noPeople, Guid[]? tagged = null)
    {
        var key = Guid.NewGuid();
        _images.Facts[key] = new(key, taken, null, null, noPeople, tagged ?? [], []);
        return key;
    }

    [Fact]
    public async Task Image_with_no_governance_data_is_usable_and_needs_tagging()
    {
        var key = Guid.NewGuid();
        Assert.Equal(GovernanceStatus.NeedsTagging, await Sut().GetStatusAsync(key));
        Assert.True(await Sut().IsUsableAsync(key));
    }

    private class FakeUploads(Dictionary<Guid, DateOnly> dates) : IMediaUploadDates
    {
        public Task<IReadOnlyDictionary<Guid, DateOnly>> GetAsync(IReadOnlyCollection<Guid> mediaKeys) =>
            Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(dates);
    }

    [Fact]
    public async Task An_image_with_no_dates_uses_its_upload_date_for_retention()
    {
        var key = Add(null, noPeople: true);                         // no date taken or obtained
        var uploads = new FakeUploads(new() { [key] = new DateOnly(2020, 1, 1) });
        var sut = new MediaGovernanceUsage(new StatusLookupService(_images, new FakeSettings(), new FixedTimeProvider(Today), uploads));

        Assert.Equal(GovernanceStatus.Expired, await sut.GetStatusAsync(key));
        Assert.False(await sut.IsUsableAsync(key));
    }

    [Fact]
    public async Task Expired_image_is_not_usable()
    {
        var key = Add(new DateOnly(2020, 1, 1), noPeople: true);
        Assert.Equal(GovernanceStatus.Expired, await Sut().GetStatusAsync(key));
        Assert.False(await Sut().IsUsableAsync(key));
    }

    [Fact]
    public async Task Image_with_a_tagged_person_and_no_consent_is_not_usable()
    {
        var key = Add(new DateOnly(2025, 1, 1), noPeople: false, tagged: [Guid.NewGuid()]);
        Assert.False(await Sut().IsUsableAsync(key));
    }

    [Fact]
    public async Task Batch_lookup_returns_an_entry_for_every_key()
    {
        var good = Add(new DateOnly(2025, 1, 1), noPeople: true);
        var expired = Add(new DateOnly(2020, 1, 1), noPeople: true);
        var untouched = Guid.NewGuid();

        var map = await Sut().GetUsableAsync([good, expired, untouched]);

        Assert.True(map[good]);
        Assert.False(map[expired]);
        Assert.True(map[untouched]);
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public async Task Batch_lookup_tolerates_duplicate_keys()
    {
        var key = Add(new DateOnly(2025, 1, 1), noPeople: true);
        var map = await Sut().GetUsableAsync([key, key]);
        Assert.True(map[key]);
        Assert.Single(map);
    }

    [Fact]
    public async Task Empty_batch_returns_empty_map() => Assert.Empty(await Sut().GetUsableAsync([]));
}

public class MediaGovernanceTagHelperTests
{
    private class FakeUsage(GovernanceStatus status) : IMediaGovernanceUsage
    {
        public Task<GovernanceStatus> GetStatusAsync(Guid mediaKey) => Task.FromResult(status);
        public Task<bool> IsUsableAsync(Guid mediaKey) => Task.FromResult(UsagePolicy.IsUsable(status));
        public Task<IReadOnlyDictionary<Guid, bool>> GetUsableAsync(IReadOnlyCollection<Guid> mediaKeys) => throw new NotSupportedException();
    }

    private class FakeReporter : IWithheldMediaReporter
    {
        public List<(Guid Key, GovernanceStatus Status, string? Path)> Reports { get; } = new();
        public void Report(Guid mediaKey, GovernanceStatus status, string? pagePath) => Reports.Add((mediaKey, status, pagePath));
    }

    private class FakeHttpContextAccessor(string? path) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = path is null ? null : new DefaultHttpContext { Request = { Path = path } };
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

    private class FakePolicy(bool hidePages = true) : IFrontEndPolicyProvider
    {
        public Task<FrontEndPolicy> GetAsync() => Task.FromResult(new FrontEndPolicy(true, hidePages));
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

internal class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

public class WithheldMediaReporterTests
{
    private readonly ListLogger<WithheldMediaReporter> _log = new();
    private readonly MutableTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _media = Guid.NewGuid();

    private WithheldMediaReporter Sut() => new(_log, _time);

    [Fact]
    public void Logs_a_warning_naming_the_media_status_and_page()
    {
        Sut().Report(_media, GovernanceStatus.Expired, "/news/article");
        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(_media.ToString(), entry.Message);
        Assert.Contains("Expired", entry.Message);
        Assert.Contains("/news/article", entry.Message);
    }

    [Fact]
    public void Repeat_views_of_the_same_page_are_not_logged_again_within_the_window()
    {
        var sut = Sut();
        sut.Report(_media, GovernanceStatus.Expired, "/news/article");
        _time.Now = _time.Now.AddHours(1);
        sut.Report(_media, GovernanceStatus.Expired, "/news/article");
        Assert.Single(_log.Entries);
    }

    [Fact]
    public void It_is_logged_again_once_the_window_has_passed_so_it_is_not_forgotten()
    {
        var sut = Sut();
        sut.Report(_media, GovernanceStatus.Expired, "/news/article");
        _time.Now = _time.Now.AddHours(7);
        sut.Report(_media, GovernanceStatus.Expired, "/news/article");
        Assert.Equal(2, _log.Entries.Count);
    }

    [Fact]
    public void The_same_media_on_a_different_page_is_logged_separately()
    {
        var sut = Sut();
        sut.Report(_media, GovernanceStatus.Expired, "/news/a");
        sut.Report(_media, GovernanceStatus.Expired, "/news/b");
        Assert.Equal(2, _log.Entries.Count);
    }

    [Fact]
    public void A_change_of_status_on_the_same_page_is_logged_again()
    {
        var sut = Sut();
        sut.Report(_media, GovernanceStatus.NoConsent, "/news/a");
        sut.Report(_media, GovernanceStatus.Expired, "/news/a");
        Assert.Equal(2, _log.Entries.Count);
    }

    [Fact]
    public void Without_a_page_path_the_media_is_still_reported()
    {
        Sut().Report(_media, GovernanceStatus.Expired, null);
        Assert.Single(_log.Entries);
    }
}
