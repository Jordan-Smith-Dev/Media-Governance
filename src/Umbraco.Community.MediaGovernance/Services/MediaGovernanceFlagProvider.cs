using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Api.Management.Services.Flags;
using Umbraco.Cms.Api.Management.ViewModels;
using Umbraco.Cms.Api.Management.ViewModels.Media.Collection;
using Umbraco.Cms.Api.Management.ViewModels.Tree;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>
/// Flags image items in the media tree with their governance status. The matching backoffice
/// <c>entitySign</c> extensions turn the flags into indicators.
/// </summary>
public class MediaGovernanceFlagProvider(IServiceScopeFactory scopes) : IFlagProvider
{
    public const string FlagPrefix = "Umbraco.Community.MediaGovernance.";

    private Guid? _imageTypeKey;
    private Guid? _folderTypeKey;

    /// <summary>Marks folders that contain images that cannot be used (red) or need attention (amber).</summary>
    private async Task MarkFoldersAsync(IServiceProvider services, List<MediaCollectionResponseModel> items)
    {
        var folderTypeKey = _folderTypeKey ??= services.GetRequiredService<IMediaTypeService>().Get("Folder")?.Key;
        if (folderTypeKey is null) return;

        var folders = items.Where(i => i.MediaType.Id == folderTypeKey).ToList();
        if (folders.Count == 0) return;

        var attention = await services.GetRequiredService<FolderIssuesService>().GetAsync(folders.Select(f => f.Id).ToList());
        foreach (var folder in folders)
        {
            if (!attention.TryGetValue(folder.Id, out var level) || level == FolderAttention.None) continue;
            folder.AddFlag(FlagPrefix + (level == FolderAttention.Withheld ? "FolderWithheld" : "FolderWarning"));
        }
    }

    public bool CanProvideFlags<TItem>() where TItem : IHasFlags => typeof(TItem) == typeof(MediaTreeItemResponseModel) || typeof(TItem) == typeof(MediaCollectionResponseModel);

    public async Task PopulateFlagsAsync<TItem>(IEnumerable<TItem> itemViewModels) where TItem : IHasFlags
    {
        // Folder markers only appear in the collection (card) view; the tree keeps folders as they are.
        var cards = itemViewModels.OfType<MediaCollectionResponseModel>().ToList();
        if (cards.Count > 0)
        {
            using var cardScope = scopes.CreateScope();
            await MarkFoldersAsync(cardScope.ServiceProvider, cards);
            await FlagImagesAsync(cardScope.ServiceProvider, cards.Select(c => (c.Id, c.MediaType.Id, (Action<string>)(f => c.AddFlag(f)), (Action<string>)(_ => { }))).ToList());
        }

        var items = itemViewModels.OfType<MediaTreeItemResponseModel>().ToList();
        if (items.Count == 0) return;

        using var scope = scopes.CreateScope();
        await FlagImagesAsync(scope.ServiceProvider, items.Select(i => (i.Id, i.MediaType.Id, (Action<string>)(f => i.AddFlag(f)),
            (Action<string>)(color => i.MediaType.Icon = TreeIconColor.Apply(i.MediaType.Icon, color) ?? i.MediaType.Icon))).ToList());
    }

    /// <summary>Adds one flag per problem to each image, and colours its icon where the host shows one.</summary>
    private async Task FlagImagesAsync(IServiceProvider services,
        List<(Guid Id, Guid TypeId, Action<string> AddFlag, Action<string> SetIconColor)> items)
    {
        var imageTypeKey = _imageTypeKey ??= services.GetRequiredService<IMediaTypeService>().Get(ImageCatalog.ImageTypeAlias)?.Key;
        if (imageTypeKey is null) return;

        var images = items.Where(i => i.TypeId == imageTypeKey).ToList();
        if (images.Count == 0) return;
        var issuesByImage = await services.GetRequiredService<StatusLookupService>().GetIssuesAsync(images.Select(i => i.Id).ToList());

        foreach (var item in images)
        {
            if (!issuesByImage.TryGetValue(item.Id, out var issues)) continue;

            // One sign per problem, so an image that is both expired and missing consent shows (and explains) both.
            foreach (var issue in issues)
                item.AddFlag(FlagPrefix + issue);
            var status = issues.Count == 0 ? GovernanceStatus.Compliant : issues[0];

            var color = TreeIconColor.For(status);
            if (color is not null)
                item.SetIconColor(color);
        }
    }
}
