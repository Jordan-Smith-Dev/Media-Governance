using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Jobs;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class TransitionDetectorTests
{
    private static readonly Guid A = Guid.NewGuid();

    private static IReadOnlyList<Transition> Detect(GovernanceStatus current, GovernanceStatus? last) =>
        TransitionDetector.Detect([(A, current, last)]);

    [Theory]
    [InlineData(GovernanceStatus.ExpiringSoon)]
    [InlineData(GovernanceStatus.Expired)]
    [InlineData(GovernanceStatus.NoConsent)]
    [InlineData(GovernanceStatus.NeedsTagging)]
    public void Notifies_once_on_entering_an_attention_status(GovernanceStatus status)
    {
        var t = Assert.Single(Detect(status, GovernanceStatus.Compliant));
        Assert.Equal(A, t.MediaKey);
        Assert.Equal(status, t.To);
    }

    [Fact]
    public void Notifies_for_first_sighting_when_never_notified() =>
        Assert.Single(Detect(GovernanceStatus.Expired, null));

    [Theory]
    [InlineData(GovernanceStatus.ExpiringSoon)]
    [InlineData(GovernanceStatus.Expired)]
    [InlineData(GovernanceStatus.NoConsent)]
    [InlineData(GovernanceStatus.NeedsTagging)]
    public void Does_not_repeat_for_unchanged_status(GovernanceStatus status) =>
        Assert.Empty(Detect(status, status));

    [Theory]
    [InlineData(GovernanceStatus.Compliant)]
    public void Does_not_notify_for_non_attention_statuses(GovernanceStatus status) =>
        Assert.Empty(Detect(status, GovernanceStatus.Expired));

    [Fact]
    public void Notifies_again_after_status_returns_and_changes_again()
    {
        // Compliant was recorded in between (see TransitionDetector.StatusesToRecord), so a new Expired re-notifies.
        Assert.Single(Detect(GovernanceStatus.Expired, GovernanceStatus.Compliant));
    }

    [Fact]
    public void Records_every_changed_status_even_when_not_notified()
    {
        var toRecord = TransitionDetector.StatusesToRecord([(A, GovernanceStatus.Compliant, GovernanceStatus.Expired)]);
        Assert.Equal(GovernanceStatus.Compliant, toRecord[A]);
    }

    [Fact]
    public void Unsent_attention_transitions_are_not_recorded_so_they_are_retried()
    {
        var other = Guid.NewGuid();
        var items = new (Guid, GovernanceStatus, GovernanceStatus?)[]
        {
            (A, GovernanceStatus.Expired, null),                            // attention transition, not sent
            (other, GovernanceStatus.Compliant, GovernanceStatus.Expired)   // plain change
        };
        var recorded = TransitionDetector.StatusesToRecord(items, notificationSent: false);
        Assert.DoesNotContain(A, recorded.Keys);
        Assert.Equal(GovernanceStatus.Compliant, recorded[other]);
        Assert.Equal(GovernanceStatus.Expired, TransitionDetector.StatusesToRecord(items, notificationSent: true)[A]);
    }
}

public class GovernanceOptionsValidatorTests
{
    private static SettingsUpdate Update(params string[] emails) => new(3, 90, emails);

    [Fact]
    public void Accepts_valid_settings() => GovernanceOptionsValidator.Validate(Update("ops@example.com"));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b.com, c@d.com")]
    [InlineData("")]
    public void Rejects_invalid_email_addresses(string email) =>
        Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(Update("ops@example.com", email)));

    [Fact]
    public void Normalize_trims_and_removes_duplicates_ignoring_case()
    {
        var cleaned = GovernanceOptionsValidator.Normalize(Update(" Ops@Example.com ", "ops@example.com", "", "  "));
        Assert.Equal(["Ops@Example.com"], cleaned.NotificationEmails);
    }

    [Fact]
    public void Rejects_more_than_25_recipients()
    {
        var many = Enumerable.Range(1, 26).Select(i => $"user{i}@example.com").ToArray();
        Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(Update(many)));
    }

    [Fact]
    public void Accepts_exactly_25_recipients()
    {
        var many = Enumerable.Range(1, 25).Select(i => $"user{i}@example.com").ToArray();
        GovernanceOptionsValidator.Validate(Update(many));
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(101, 90)]
    [InlineData(3, -1)]
    [InlineData(3, 3651)]
    public void Rejects_out_of_range_retention_or_warning(int years, int days) =>
        Assert.Throws<ArgumentException>(() => GovernanceOptionsValidator.Validate(new SettingsUpdate(years, days, [])));
}
