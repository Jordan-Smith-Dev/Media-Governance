using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

public class AffectedPagesServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);

    private readonly FakeImageRepository _images = new();
    private readonly FakeReferences _references = new();
    private readonly List<ImageInfo> _catalog = new();

    private class FakeReferences : IMediaReferenceSource
    {
        public Dictionary<Guid, List<MediaReferrer>> Pages { get; } = new();
        public Task<IReadOnlyList<MediaReferrer>> GetPagesUsingAsync(Guid mediaKey) =>
            Task.FromResult<IReadOnlyList<MediaReferrer>>(Pages.GetValueOrDefault(mediaKey) ?? new());
    }

    private AffectedPagesService Sut() => new(
        new FakeCatalog(_catalog.ToArray()),
        new StatusLookupService(_images, new FakeSettings(), new FixedTimeProvider(Today)),
        _references);

    private ImageInfo AddImage(string name, GovernanceStatus wanted)
    {
        var info = new ImageInfo(Guid.NewGuid(), name, "Folder");
        _catalog.Add(info);
        var tagged = wanted == GovernanceStatus.NoConsent ? new[] { Guid.NewGuid() } : [];
        var taken = wanted == GovernanceStatus.Expired ? new DateOnly(2020, 1, 1) : new DateOnly(2025, 6, 1);
        _images.Facts[info.Key] = new(info.Key, taken, null, null, wanted != GovernanceStatus.NoConsent, tagged, []);
        return info;
    }

    private static MediaReferrer Page(Guid key, string name, bool published = true) => new(key, name, published, "Article", "icon-document");

    [Fact]
    public async Task No_withheld_images_means_no_pages()
    {
        var ok = AddImage("Fine", GovernanceStatus.Compliant);
        _references.Pages[ok.Key] = [Page(Guid.NewGuid(), "Home")];

        Assert.Empty(await Sut().GetAsync());
    }

    [Fact]
    public async Task A_page_using_a_withheld_image_is_listed_with_that_image_and_its_status()
    {
        var expired = AddImage("Campus aerial", GovernanceStatus.Expired);
        var home = Guid.NewGuid();
        _references.Pages[expired.Key] = [Page(home, "Home")];

        var page = Assert.Single(await Sut().GetAsync());
        Assert.Equal(home, page.PageKey);
        Assert.Equal("Home", page.Name);
        var image = Assert.Single(page.Images);
        Assert.Equal(expired.Key, image.MediaKey);
        Assert.Equal("Campus aerial", image.Name);
        Assert.Equal(GovernanceStatus.Expired, image.Status);
    }

    [Fact]
    public async Task A_page_using_several_withheld_images_appears_once_with_all_of_them()
    {
        var a = AddImage("B image", GovernanceStatus.Expired);
        var b = AddImage("A image", GovernanceStatus.NoConsent);
        var home = Guid.NewGuid();
        _references.Pages[a.Key] = [Page(home, "Home")];
        _references.Pages[b.Key] = [Page(home, "Home")];

        var page = Assert.Single(await Sut().GetAsync());
        Assert.Equal(["A image", "B image"], page.Images.Select(i => i.Name));
    }

    [Fact]
    public async Task Usable_images_on_the_same_page_are_not_listed()
    {
        var bad = AddImage("Bad", GovernanceStatus.Expired);
        var good = AddImage("Good", GovernanceStatus.Compliant);
        var home = Guid.NewGuid();
        _references.Pages[bad.Key] = [Page(home, "Home")];
        _references.Pages[good.Key] = [Page(home, "Home")];

        var page = Assert.Single(await Sut().GetAsync());
        Assert.Equal("Bad", Assert.Single(page.Images).Name);
    }

    [Fact]
    public async Task Withheld_image_used_nowhere_adds_no_page()
    {
        AddImage("Unused", GovernanceStatus.Expired);
        Assert.Empty(await Sut().GetAsync());
    }

    [Fact]
    public async Task Pages_are_ordered_by_name_and_keep_their_published_state()
    {
        var img = AddImage("Bad", GovernanceStatus.Expired);
        _references.Pages[img.Key] = [Page(Guid.NewGuid(), "Zebra", published: false), Page(Guid.NewGuid(), "Apple")];

        var pages = await Sut().GetAsync();
        Assert.Equal(["Apple", "Zebra"], pages.Select(p => p.Name));
        Assert.False(pages[1].Published);
    }
}
