using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Jobs;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class DigestBuilderTests
{
    private static (IReadOnlyList<Transition> Transitions, IReadOnlyDictionary<Guid, string> Names) Make(
        params (string Name, GovernanceStatus Status)[] items)
    {
        var names = new Dictionary<Guid, string>();
        var transitions = new List<Transition>();
        foreach (var (name, status) in items)
        {
            var key = Guid.NewGuid();
            names[key] = name;
            transitions.Add(new Transition(key, status));
        }
        return (transitions, names);
    }

    [Fact]
    public void Each_status_gets_its_own_heading_with_its_images()
    {
        var (t, n) = Make(("Aerial", GovernanceStatus.Expired), ("Lecture", GovernanceStatus.NoConsent), ("Sports", GovernanceStatus.NeedsTagging));
        var html = DigestBuilder.Build(t, n);
        Assert.Contains("<h3>Expired</h3>", html);
        Assert.Contains("<h3>No consent</h3>", html);
        Assert.Contains("<h3>Needs tagging</h3>", html);
        Assert.Contains("<li>Sports</li>", html);
    }

    [Fact]
    public void Groups_are_ordered_by_severity_regardless_of_input_order()
    {
        var (t, n) = Make(("A", GovernanceStatus.NeedsTagging), ("B", GovernanceStatus.ExpiringSoon), ("C", GovernanceStatus.Expired), ("D", GovernanceStatus.NoConsent));
        var html = DigestBuilder.Build(t, n);
        var order = new[] { "Expired", "No consent", "Needs tagging", "Expiring soon" }.Select(h => html.IndexOf($"<h3>{h}</h3>", StringComparison.Ordinal)).ToArray();
        Assert.Equal(order.OrderBy(i => i), order);
        Assert.All(order, i => Assert.True(i >= 0));
    }

    [Fact]
    public void Image_names_are_html_encoded()
    {
        var (t, n) = Make(("<script>alert(1)</script>", GovernanceStatus.Expired));
        var html = DigestBuilder.Build(t, n);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void A_long_group_is_capped_and_says_how_many_more()
    {
        var items = Enumerable.Range(1, 60).Select(i => ($"Image {i}", GovernanceStatus.NeedsTagging)).ToArray();
        var (t, n) = Make(items);
        var html = DigestBuilder.Build(t, n);
        Assert.Contains("<li>Image 50</li>", html);
        Assert.DoesNotContain("<li>Image 51</li>", html);
        Assert.Contains("and 10 more", html);
    }

    [Fact]
    public void An_unknown_image_falls_back_to_its_key()
    {
        var key = Guid.NewGuid();
        var html = DigestBuilder.Build([new Transition(key, GovernanceStatus.Expired)], new Dictionary<Guid, string>());
        Assert.Contains(key.ToString(), html);
    }
}
