using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.TestSite;

/// <summary>Seeds the test site with the 12 images and 6 people from the design prototype (test site only; never part of the package).</summary>
public class SeedDataComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SeedDataHandler>();
}

public class SeedDataHandler(IServiceScopeFactory scopes, IWebHostEnvironment web) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string Actor = "seed";

    private record Img(string Folder, string Name, DateOnly? Taken, DateOnly? Custom, bool NoPeople, string[] People, string[] Consented);

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static readonly Img[] Images =
    [
        new("Campus", "Library exterior", D(2024, 3, 2), null, true, [], []),
        new("Campus", "Lecture theatre", D(2025, 1, 15), null, false, ["Tom Reyes"], []),
        new("Campus", "Science lab", D(2023, 12, 20), null, true, [], []),
        new("Campus", "Campus aerial", D(2022, 9, 30), null, true, [], []),
        new("Events", "Open day crowd", D(2023, 11, 20), null, false, ["Amara Okafor", "Priya Shah"], ["Amara Okafor", "Priya Shah"]),
        new("Events", "Graduation 2023", D(2023, 7, 14), null, false, ["Verna Burke", "Jonas Weber"], ["Verna Burke", "Jonas Weber"]),
        new("Events", "Sports day", D(2025, 6, 1), null, false, [], []),
        new("Events", "Welcome week", D(2025, 9, 15), null, true, [], []),
        new("Marketing", "Student ambassadors", D(2025, 9, 20), null, true, [], []),
        new("Marketing", "Prospectus cover", D(2024, 10, 1), D(2026, 12, 31), false, ["Bentley Milford"], ["Bentley Milford"]),
        new("Staff", "Headshot - Verna Burke", D(2024, 5, 10), null, false, ["Verna Burke"], ["Verna Burke"]),
        new("Staff", "Team offsite", D(2023, 12, 1), null, false, ["Bentley Milford", "Priya Shah"], []),
    ];

    private static readonly string[] PeopleNames =
        ["Verna Burke", "Amara Okafor", "Tom Reyes", "Bentley Milford", "Priya Shah", "Jonas Weber"];

    // A plain light-grey PNG, so the seeded images have a real file that can be requested.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAKAAAAB4CAIAAAD6wG44AAABL0lEQVR4nO3RwQkAIBDAsNt/QD/+nMQpRCiBDFDorH0Im+8FPGVwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxBscZHGdwnMFxF3JXHFAZOlO7AAAAAElFTkSuQmCC");

    private void WriteSeedFiles()
    {
        var folder = Path.Combine(web.WebRootPath, "media", "seed");
        Directory.CreateDirectory(folder);
        foreach (var img in Images)
        {
            var file = Path.Combine(folder, img.Name.ToLowerInvariant().Replace(' ', '-') + ".png");
            if (!System.IO.File.Exists(file)) System.IO.File.WriteAllBytes(file, Png);
        }
    }

    private static readonly (string Name, string[] Images, bool Publish)[] Pages =
    [
        ("Campus news", ["Campus aerial", "Library exterior"], true),    // one withheld (expired), one fine
        ("Open day", ["Lecture theatre", "Campus aerial"], true),        // two withheld (no consent, expired)
        ("Welcome page", ["Welcome week"], false),                       // fine: must not be listed
    ];

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        await SeedCoreAsync();
        await SeedRootImagesAsync();
        await SeedPagesAsync();
    }

    /// <summary>Images that sit directly in the media root, not in a folder (to prove folders are only organisation).</summary>
    private async Task SeedRootImagesAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var media = sp.GetRequiredService<IMediaService>();
        var existing = media.GetRootMedia().Select(m => m.Name).ToHashSet();
        var images = sp.GetRequiredService<ImageGovernanceService>();
        var index = sp.GetRequiredService<IMediaFileIndex>();
        var folder = Path.Combine(web.WebRootPath, "media", "seed");
        Directory.CreateDirectory(folder);

        // name, date taken (null = none), years ago it was uploaded
        (string Name, DateOnly? Taken, int UploadedYearsAgo)[] roots =
        [
            ("Root logo", D(2025, 1, 1), 0),     // fine
            ("Root banner", D(2020, 1, 1), 0),   // expired by its date taken
            ("Root old upload", null, 5),        // no dates at all: judged by an upload date five years ago
        ];

        foreach (var (name, taken, yearsAgo) in roots)
        {
            var slug = name.ToLowerInvariant().Replace(' ', '-');
            var file = Path.Combine(folder, slug + ".png");
            if (!System.IO.File.Exists(file)) System.IO.File.WriteAllBytes(file, Png);   // every start, like the other seed files

            if (existing.Contains(name!)) continue;

            var item = media.CreateMedia(name, Umbraco.Cms.Core.Constants.System.Root, "Image");
            item.SetValue("umbracoFile", $"{{\"src\":\"/media/seed/{slug}.png\"}}");
            if (yearsAgo > 0) item.CreateDate = DateTime.Now.AddYears(-yearsAgo);
            media.Save(item);

            if (taken is not null)
                await images.SaveAsync(item.Key, new ImageGovernanceUpdate(taken, null, null, true, []), Actor);
            await index.RefreshAsync(item.Key);
        }
    }

    /// <summary>A small content type with an image picker, and pages that use some withheld images (for "Pages affected").</summary>
    private async Task SeedPagesAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var contentTypes = sp.GetRequiredService<IContentTypeService>();
        if (contentTypes.Get("governanceDemoPage") is not null) return;

        var picker = await sp.GetRequiredService<IDataTypeService>().GetAsync("Image Media Picker");
        if (picker is null) return;

        var shortString = sp.GetRequiredService<IShortStringHelper>();
        var type = new ContentType(shortString, Umbraco.Cms.Core.Constants.System.Root)
        {
            Alias = "governanceDemoPage",
            Name = "Governance Demo Page",
            Icon = "icon-document",
            AllowedAsRoot = true
        };
        type.AddPropertyType(new PropertyType(shortString, picker) { Alias = "image", Name = "Image" }, "content", "Content");
        await contentTypes.CreateAsync(type, Umbraco.Cms.Core.Constants.Security.SuperUserKey);

        var catalog = await sp.GetRequiredService<IImageCatalog>().ListImagesAsync();
        var content = sp.GetRequiredService<IContentService>();
        foreach (var (name, imageNames, publish) in Pages)
        {
            var picks = imageNames
                .Select(n => catalog.FirstOrDefault(c => c.Name == n))
                .Where(c => c is not null)
                .Select(c => $"{{\"key\":\"{Guid.NewGuid()}\",\"mediaKey\":\"{c!.Key}\",\"mediaTypeAlias\":\"\",\"crops\":[],\"focalPoint\":null}}");
            var page = content.Create(name, Umbraco.Cms.Core.Constants.System.Root, "governanceDemoPage");
            page.SetValue("image", "[" + string.Join(",", picks) + "]");
            content.Save(page);
            if (publish) content.Publish(page, ["*"]);
        }
    }

    private async Task SeedCoreAsync()
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var people = sp.GetRequiredService<PersonService>();
        WriteSeedFiles();   // every start, so the files exist even when the data was seeded earlier
        if ((await people.ListAsync(null)).Count > 0)
            return; // already seeded

        var media = sp.GetRequiredService<IMediaService>();
        var images = sp.GetRequiredService<ImageGovernanceService>();
        var consents = sp.GetRequiredService<ConsentService>();

        var personIds = new Dictionary<string, Guid>();
        foreach (var name in PeopleNames)
            personIds[name] = (await people.CreateAsync(
                new PersonInput(name, $"{name.Split(' ')[0].ToLowerInvariant()}@example.com", null, null, null), Actor)).Id;

        var folders = Images.Select(i => i.Folder).Distinct().ToDictionary(f => f, f =>
        {
            var folder = media.CreateMedia(f, Umbraco.Cms.Core.Constants.System.Root, "Folder");
            media.Save(folder);
            return folder;
        });

        foreach (var img in Images)
        {
            var item = media.CreateMedia(img.Name, folders[img.Folder], "Image");
            item.SetValue("umbracoFile", $"{{\"src\":\"/media/seed/{img.Name.ToLowerInvariant().Replace(' ', '-')}.png\"}}");
            media.Save(item);

            var tagged = img.People.Select(p => personIds[p]).ToList();
            await sp.GetRequiredService<IMediaFileIndex>().RefreshAsync(item.Key);
            await images.SaveAsync(item.Key, new ImageGovernanceUpdate(img.Taken, null, img.Custom, img.NoPeople, tagged), Actor);

            foreach (var name in img.Consented)
                await consents.AddAsync(item.Key,
                    new ConsentInput(personIds[name], D(2024, 1, 10), ConsentGivenBy.Self, ["Web", "Print"], null, null), Actor);
        }
    }
}
