using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Tests;

public class TreeIconColorTests
{
    [Theory]
    [InlineData(GovernanceStatus.Expired, "red")]
    [InlineData(GovernanceStatus.NoConsent, "red")]
    [InlineData(GovernanceStatus.ExpiringSoon, "brown")]
    [InlineData(GovernanceStatus.Compliant, "green")]
    public void Each_status_has_a_colour(GovernanceStatus status, string color) =>
        Assert.Equal(color, TreeIconColor.For(status));

    [Fact]
    public void Images_that_need_tagging_keep_the_normal_icon_colour() =>
        Assert.Null(TreeIconColor.For(GovernanceStatus.NeedsTagging));

    [Theory]
    [InlineData("icon-picture", "red", "icon-picture color-red")]
    [InlineData("icon-picture color-blue", "red", "icon-picture color-red")]
    [InlineData("  icon-picture  ", "green", "icon-picture color-green")]
    public void Apply_replaces_any_existing_colour_on_the_icon(string icon, string color, string expected) =>
        Assert.Equal(expected, TreeIconColor.Apply(icon, color));

    [Fact]
    public void Apply_leaves_the_icon_alone_when_there_is_no_colour_or_no_icon()
    {
        Assert.Equal("icon-picture color-blue", TreeIconColor.Apply("icon-picture color-blue", null));
        Assert.Null(TreeIconColor.Apply(null, "red"));
    }
}
