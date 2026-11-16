using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class MediaFilePathTests
{
    [Theory]
    [InlineData("/media/abc123/Photo.JPG", "media/abc123/photo.jpg")]
    [InlineData("/media/abc123/photo%20one.jpg", "media/abc123/photo one.jpg")]
    [InlineData("/media/abc123/photo.jpg?width=200&height=100", "media/abc123/photo.jpg")]
    [InlineData("/media/abc123/photo.jpg#frag", "media/abc123/photo.jpg")]
    [InlineData("\\media\\abc123\\photo.jpg", "media/abc123/photo.jpg")]
    [InlineData("media/abc123/photo.jpg", "media/abc123/photo.jpg")]
    public void Normalize_makes_request_and_stored_paths_comparable(string input, string expected) =>
        Assert.Equal(expected, MediaFilePath.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?width=100")]
    public void Normalize_returns_null_when_there_is_no_path(string? input) =>
        Assert.Null(MediaFilePath.Normalize(input));

    [Fact]
    public void Normalize_does_not_let_dot_segments_escape_the_media_folder() =>
        Assert.Equal("etc/passwd", MediaFilePath.Normalize("/media/../etc/passwd"));

    [Theory]
    [InlineData("{\"src\":\"/media/abc/photo.png\",\"crops\":[]}", "/media/abc/photo.png")]
    [InlineData("{\"src\":\"/media/abc/photo.png\"}", "/media/abc/photo.png")]
    [InlineData("/media/abc/photo.png", "/media/abc/photo.png")]
    public void ExtractSrc_reads_both_json_and_plain_umbracoFile_values(string value, string expected) =>
        Assert.Equal(expected, MediaFilePath.ExtractSrc(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{\"crops\":[]}")]
    [InlineData("just text")]
    public void ExtractSrc_returns_null_for_missing_or_unusable_values(string? value) =>
        Assert.Null(MediaFilePath.ExtractSrc(value));
}

public class MediaFileGateTests
{
    private readonly Guid _media = Guid.NewGuid();
    private readonly FakeIndex _index = new();
    private readonly FakeUsageStatus _usage = new();
    private readonly FakeReporterForGate _reporter = new();

    private class FakeIndex : IMediaFileIndex
    {
        public Dictionary<string, Guid> Paths { get; } = new();
        public int Lookups { get; private set; }
        public Task<Guid?> FindMediaKeyAsync(string normalizedPath) { Lookups++; return Task.FromResult(Paths.TryGetValue(normalizedPath, out var k) ? (Guid?)k : null); }
        public Task RefreshAsync(Guid mediaKey) => Task.CompletedTask;
        public Task RefreshAllAsync() => Task.CompletedTask;
        public void ClearCache() { }
    }

    private class FakeUsageStatus : IMediaGovernanceUsage
    {
        public GovernanceStatus Status { get; set; } = GovernanceStatus.Compliant;
        public int Calls { get; private set; }
        public Task<GovernanceStatus> GetStatusAsync(Guid mediaKey) { Calls++; return Task.FromResult(Status); }
        public Task<bool> IsUsableAsync(Guid mediaKey) => Task.FromResult(UsagePolicy.IsUsable(Status));
        public Task<IReadOnlyDictionary<Guid, bool>> GetUsableAsync(IReadOnlyCollection<Guid> keys) => throw new NotSupportedException();
    }

    private class FakeReporterForGate : IWithheldMediaReporter
    {
        public List<(Guid Key, GovernanceStatus Status, string? Path)> Reports { get; } = new();
        public void Report(Guid mediaKey, GovernanceStatus status, string? pagePath) => Reports.Add((mediaKey, status, pagePath));
    }

    private MediaFileGate Sut()
    {
        _index.Paths["media/abc/photo.jpg"] = _media;
        return new MediaFileGate(_index, _usage, _reporter, new MediaGateCache());
    }

    [Fact]
    public async Task A_file_that_is_not_governed_is_allowed()
    {
        var decision = await Sut().CheckAsync("/media/other/unknown.jpg", isBackofficeUser: false);
        Assert.True(decision.Allowed);
        Assert.Equal(0, _usage.Calls);
    }

    [Theory]
    [InlineData(GovernanceStatus.Compliant)]
    [InlineData(GovernanceStatus.ExpiringSoon)]
    [InlineData(GovernanceStatus.NeedsTagging)]
    public async Task A_usable_image_file_is_allowed(GovernanceStatus status)
    {
        _usage.Status = status;
        Assert.True((await Sut().CheckAsync("/media/abc/photo.jpg", false)).Allowed);
        Assert.Empty(_reporter.Reports);
    }

    [Theory]
    [InlineData(GovernanceStatus.Expired)]
    [InlineData(GovernanceStatus.NoConsent)]
    public async Task A_withheld_image_file_is_blocked_and_reported(GovernanceStatus status)
    {
        _usage.Status = status;
        var decision = await Sut().CheckAsync("/media/abc/photo.jpg", false);
        Assert.False(decision.Allowed);
        Assert.Equal(status, decision.Status);
        var report = Assert.Single(_reporter.Reports);
        Assert.Equal(_media, report.Key);
        Assert.Contains("/media/abc/photo.jpg", report.Path);
    }

    [Fact]
    public async Task Resized_and_differently_cased_requests_for_a_withheld_image_are_blocked_too()
    {
        _usage.Status = GovernanceStatus.Expired;
        var sut = Sut();
        Assert.False((await sut.CheckAsync("/media/abc/photo.jpg?width=100&height=100", false)).Allowed);
        Assert.False((await sut.CheckAsync("/MEDIA/ABC/Photo.JPG", false)).Allowed);
    }

    [Fact]
    public async Task Backoffice_users_can_always_see_the_file_so_editors_can_fix_the_image()
    {
        _usage.Status = GovernanceStatus.Expired;
        var decision = await Sut().CheckAsync("/media/abc/photo.jpg", isBackofficeUser: true);
        Assert.True(decision.Allowed);
        Assert.Equal(0, _index.Lookups);
    }

    [Fact]
    public async Task Repeat_requests_use_the_short_cache_instead_of_the_database()
    {
        var sut = Sut();
        await sut.CheckAsync("/media/abc/photo.jpg", false);
        await sut.CheckAsync("/media/abc/photo.jpg", false);
        Assert.Equal(1, _usage.Calls);
    }

    [Fact]
    public async Task Clearing_the_cache_makes_a_governance_change_take_effect_immediately()
    {
        var sut = Sut();
        Assert.True((await sut.CheckAsync("/media/abc/photo.jpg", false)).Allowed);

        _usage.Status = GovernanceStatus.NoConsent;   // consent withdrawn
        sut.ClearCache();

        Assert.False((await sut.CheckAsync("/media/abc/photo.jpg", false)).Allowed);
    }
}
