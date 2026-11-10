using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Tests;

public class StatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);
    private static readonly GovernanceSettings Settings = new(3, 90);
    private static readonly Guid P1 = Guid.NewGuid();
    private static readonly Guid P2 = Guid.NewGuid();

    private static ImageGovernanceFacts Facts(
        DateOnly? taken = null, DateOnly? obtained = null, DateOnly? custom = null,
        bool noPeople = false, Guid[]? tagged = null, params ConsentFacts[] consents) =>
        new(Guid.NewGuid(), taken, obtained, custom, noPeople, tagged ?? [], consents);

    private static ConsentFacts Consent(Guid person, DateOnly? expires = null, DateOnly? withdrawn = null) =>
        new(person, new DateOnly(2024, 1, 1), expires, withdrawn);

    private static GovernanceStatus Status(ImageGovernanceFacts f) =>
        StatusCalculator.Calculate(f, Settings, Today);

    [Fact]
    public void Expired_when_expiry_is_today() =>
        Assert.Equal(GovernanceStatus.Expired, Status(Facts(taken: new(2023, 10, 29), noPeople: true)));

    [Fact]
    public void ExpiringSoon_when_within_warning_window() =>
        Assert.Equal(GovernanceStatus.ExpiringSoon, Status(Facts(taken: new(2023, 12, 1), noPeople: true)));

    [Fact]
    public void ExpiringSoon_boundary_is_inclusive_of_last_warning_day()
    {
        // expiry = today + 90 days = 2027-01-27 -> taken 2024-01-27
        Assert.Equal(GovernanceStatus.ExpiringSoon, Status(Facts(taken: new(2024, 1, 27), noPeople: true)));
        Assert.Equal(GovernanceStatus.Compliant, Status(Facts(taken: new(2024, 1, 28), noPeople: true)));
    }

    [Fact]
    public void NoConsent_when_person_tagged_without_consent() =>
        Assert.Equal(GovernanceStatus.NoConsent, Status(Facts(taken: new(2025, 1, 1), tagged: [P1])));

    [Fact]
    public void Withdrawn_consent_is_not_active() =>
        Assert.Equal(GovernanceStatus.NoConsent,
            Status(Facts(taken: new(2025, 1, 1), tagged: [P1], consents: Consent(P1, withdrawn: new(2025, 6, 1)))));

    [Fact]
    public void Expired_consent_makes_image_Expired() =>
        Assert.Equal(GovernanceStatus.Expired,
            Status(Facts(taken: new(2025, 1, 1), tagged: [P1], consents: Consent(P1, expires: new(2026, 1, 1)))));

    [Fact]
    public void NeedsTagging_when_no_people_recorded() =>
        Assert.Equal(GovernanceStatus.NeedsTagging, Status(Facts(taken: new(2025, 1, 1))));

    [Fact]
    public void NoPeople_flag_skips_tagging_and_consent() =>
        Assert.Equal(GovernanceStatus.Compliant, Status(Facts(taken: new(2025, 1, 1), noPeople: true)));

    [Fact]
    public void Tags_override_NoPeople_flag() =>
        Assert.Equal(GovernanceStatus.NoConsent, Status(Facts(taken: new(2025, 1, 1), noPeople: true, tagged: [P1])));

    [Fact]
    public void CustomExpiry_overrides_default_retention()
    {
        var f = Facts(taken: new(2020, 1, 1), custom: new(2028, 1, 1), noPeople: true);
        Assert.Equal(new DateOnly(2028, 1, 1), StatusCalculator.EffectiveExpiry(f, Settings));
        Assert.Equal(GovernanceStatus.Compliant, Status(f));
    }

    [Fact]
    public void Falls_back_to_DateObtained() =>
        Assert.Equal(new DateOnly(2028, 5, 5),
            StatusCalculator.EffectiveExpiry(Facts(obtained: new(2025, 5, 5), noPeople: true), Settings));

    private static ImageGovernanceFacts WithUpload(ImageGovernanceFacts f, DateOnly uploaded) => f with { DateUploaded = uploaded };

    [Fact]
    public void Falls_back_to_the_upload_date_when_there_is_no_date_taken_or_obtained()
    {
        var f = WithUpload(Facts(noPeople: true), new(2025, 5, 5));
        Assert.Equal(new DateOnly(2028, 5, 5), StatusCalculator.EffectiveExpiry(f, Settings));
        Assert.Equal(ExpirySource.Default, StatusCalculator.ExpirySourceOf(f, Settings));
    }

    [Fact]
    public void Date_taken_and_date_obtained_both_beat_the_upload_date()
    {
        Assert.Equal(new DateOnly(2027, 1, 1),
            StatusCalculator.EffectiveExpiry(WithUpload(Facts(taken: new(2024, 1, 1), noPeople: true), new(2025, 5, 5)), Settings));
        Assert.Equal(new DateOnly(2026, 2, 2),
            StatusCalculator.EffectiveExpiry(WithUpload(Facts(obtained: new(2023, 2, 2), noPeople: true), new(2025, 5, 5)), Settings));
    }

    [Fact]
    public void An_old_upload_with_no_dates_is_expired_not_ignored()
    {
        var f = WithUpload(Facts(noPeople: true), new(2020, 1, 1));
        Assert.Equal(GovernanceStatus.Expired, StatusCalculator.Calculate(f, Settings, Today));
    }

    [Fact]
    public void No_dates_means_no_expiry_and_never_expired()
    {
        var f = Facts(noPeople: true);
        Assert.Null(StatusCalculator.EffectiveExpiry(f, Settings));
        Assert.Equal(GovernanceStatus.Compliant, Status(f));
    }

    [Fact]
    public void Earliest_person_consent_expiry_caps_effective_expiry()
    {
        var f = Facts(taken: new(2025, 1, 1), tagged: [P1, P2],
            consents: [Consent(P1, expires: new(2027, 6, 1)), Consent(P2, expires: new(2028, 6, 1))]);
        Assert.Equal(new DateOnly(2027, 6, 1), StatusCalculator.EffectiveExpiry(f, Settings));
    }

    [Fact]
    public void Expired_beats_NoConsent() =>
        Assert.Equal(GovernanceStatus.Expired, Status(Facts(taken: new(2020, 1, 1), tagged: [P1])));

    [Fact]
    public void Every_tagged_person_needs_their_own_active_consent() =>
        Assert.Equal(GovernanceStatus.NoConsent,
            Status(Facts(taken: new(2025, 1, 1), tagged: [P1, P2], consents: Consent(P1))));

    [Fact]
    public void ExpirySource_says_where_the_expiry_comes_from()
    {
        Assert.Equal(ExpirySource.Custom, StatusCalculator.ExpirySourceOf(Facts(taken: new(2025, 1, 1), custom: new(2030, 1, 1), noPeople: true), Settings));
        Assert.Equal(ExpirySource.Default, StatusCalculator.ExpirySourceOf(Facts(taken: new(2025, 1, 1), noPeople: true), Settings));
        Assert.Equal(ExpirySource.None, StatusCalculator.ExpirySourceOf(Facts(noPeople: true), Settings));
    }

    [Fact]
    public void ExpirySource_is_Consent_when_a_consent_expiry_is_what_ends_use()
    {
        var f = Facts(taken: new(2025, 1, 1), tagged: [P1], consents: Consent(P1, expires: new(2026, 12, 1)));
        Assert.Equal(ExpirySource.Consent, StatusCalculator.ExpirySourceOf(f, Settings));
    }

    private static ConsentFacts ConsentFor(Guid person, params string[] uses) =>
        new(person, new DateOnly(2024, 1, 1), null, null, uses);

    private static readonly string[] AllUses = ["Web", "Print", "Social", "Marketing", "Other"];

    [Fact]
    public void ClearedUses_is_every_use_when_the_image_has_no_people()
    {
        var f = Facts(taken: new(2025, 1, 1), noPeople: true);
        Assert.Equal(AllUses, StatusCalculator.ClearedUses(f, AllUses, Today));
    }

    [Fact]
    public void ClearedUses_is_nothing_when_nobody_is_tagged_and_the_image_is_not_marked_as_having_no_people()
    {
        var f = Facts(taken: new(2025, 1, 1));
        Assert.Empty(StatusCalculator.ClearedUses(f, AllUses, Today));
    }

    [Fact]
    public void ClearedUses_is_nothing_for_a_tagged_person_without_consent()
    {
        var f = Facts(taken: new(2025, 1, 1), tagged: [P1]);
        Assert.Empty(StatusCalculator.ClearedUses(f, AllUses, Today));
    }

    [Fact]
    public void ClearedUses_is_the_uses_the_person_consented_to()
    {
        var f = Facts(taken: new(2025, 1, 1), tagged: [P1], consents: ConsentFor(P1, "Web", "Marketing"));
        Assert.Equal(["Web", "Marketing"], StatusCalculator.ClearedUses(f, AllUses, Today));
    }

    [Fact]
    public void ClearedUses_is_only_what_every_tagged_person_agreed_to()
    {
        var f = Facts(taken: new(2025, 1, 1), tagged: [P1, P2],
            consents: [ConsentFor(P1, "Web", "Print", "Marketing"), ConsentFor(P2, "Web", "Marketing")]);
        Assert.Equal(["Web", "Marketing"], StatusCalculator.ClearedUses(f, AllUses, Today));
    }

    [Fact]
    public void ClearedUses_ignores_withdrawn_and_expired_consents_and_matches_case_insensitively()
    {
        var withdrawn = new ConsentFacts(P1, new(2024, 1, 1), null, new(2025, 6, 1), ["Marketing"]);
        var expired = new ConsentFacts(P1, new(2024, 1, 1), new(2025, 6, 1), null, ["Print"]);
        var live = new ConsentFacts(P1, new(2024, 1, 1), null, null, ["web"]);
        var f = Facts(taken: new(2025, 1, 1), tagged: [P1], consents: [withdrawn, expired, live]);
        Assert.Equal(["Web"], StatusCalculator.ClearedUses(f, AllUses, Today));
    }

    [Fact]
    public void Issues_lists_every_problem_not_only_the_top_one()
    {
        // Expired AND a tagged person with no consent: both must be reported.
        var f = Facts(taken: new(2020, 1, 1), tagged: [P1]);
        Assert.Equal([GovernanceStatus.Expired, GovernanceStatus.NoConsent], StatusCalculator.Issues(f, Settings, Today));
    }

    [Fact]
    public void Issues_orders_problems_by_severity()
    {
        // Soon to expire, nobody tagged and not marked "no people".
        var f = Facts(taken: new(2023, 12, 1));
        Assert.Equal([GovernanceStatus.NeedsTagging, GovernanceStatus.ExpiringSoon], StatusCalculator.Issues(f, Settings, Today));
    }

    [Fact]
    public void Issues_is_empty_for_a_compliant_image() =>
        Assert.Empty(StatusCalculator.Issues(Facts(taken: new(2025, 1, 1), noPeople: true), Settings, Today));

    [Fact]
    public void Expired_images_are_not_also_reported_as_expiring_soon()
    {
        var f = Facts(taken: new(2020, 1, 1), noPeople: true);
        Assert.Equal([GovernanceStatus.Expired], StatusCalculator.Issues(f, Settings, Today));
    }

    [Theory]
    [InlineData(2020, false, false)]   // expired, untagged
    [InlineData(2020, true, false)]    // expired, no consent
    [InlineData(2023, false, false)]   // expiring soon, untagged
    [InlineData(2025, false, true)]    // compliant, marked no people
    [InlineData(2025, false, false)]   // needs tagging
    public void The_headline_status_is_always_the_first_issue(int year, bool tagged, bool noPeople)
    {
        var f = Facts(taken: new(year, 12, 1), noPeople: noPeople, tagged: tagged ? [P1] : null);
        var issues = StatusCalculator.Issues(f, Settings, Today);
        Assert.Equal(issues.Count == 0 ? GovernanceStatus.Compliant : issues[0], StatusCalculator.Calculate(f, Settings, Today));
    }

    [Fact]
    public void Fully_consented_image_is_Compliant() =>
        Assert.Equal(GovernanceStatus.Compliant,
            Status(Facts(taken: new(2025, 1, 1), tagged: [P1, P2], consents: [Consent(P1), Consent(P2)])));
}
