using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Controllers;

[ApiController]
[ApiVersion("1.0")]
[MapToApi(Constants.ApiName)]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
[Route($"umbraco/{Constants.ApiName}/api/v{{version:apiVersion}}")]
public class ImageController(
    ImageGovernanceService images,
    IPersonRepository personRepository,
    ISettingsService settings,
    IAuditService audit,
    IMediaFileIndex fileIndex,
    MediaGateCache gateCache,
    ImageCatalog catalog,
    IMediaUploadDates uploads,
    TimeProvider time,
    Umbraco.Cms.Core.Services.IUserService users,
    IGroupRecipients groupRecipients,
    IPhotoDates photoDates,
    IBackOfficeSecurityAccessor security,
    Microsoft.Extensions.Options.IOptions<Umbraco.Community.MediaGovernance.Middleware.MediaGovernanceOptions> appSettings) : GovernanceControllerBase(security)
{
    [HttpGet("image/{mediaKey:guid}")]
    [ProducesResponseType(typeof(ImageGovernanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid mediaKey)
    {
        if (!catalog.IsImage(mediaKey)) return NotFound();
        return Ok(await BuildAsync(mediaKey));
    }

    [HttpPut("image/{mediaKey:guid}")]
    [ProducesResponseType(typeof(ImageGovernanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(Guid mediaKey, [FromBody] ImageGovernanceRequest request)
    {
        if (!catalog.IsImage(mediaKey)) return NotFound();
        try
        {
            await images.SaveAsync(mediaKey,
                new ImageGovernanceUpdate(request.DateTaken, request.DateObtained, request.CustomExpiry, request.NoPeople, request.TaggedPersonIds),
                Actor);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        await fileIndex.RefreshAsync(mediaKey);   // also clears the file-request cache
        return Ok(await BuildAsync(mediaKey));
    }

    [HttpPost("images/bulk")]
    [ProducesResponseType(typeof(BulkImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bulk([FromBody] BulkImageRequest request)
    {
        // Only real images are changed; anything else is counted as skipped.
        var keys = (request.MediaKeys ?? []).Distinct().ToList();
        var realImages = keys.Where(catalog.IsImage).ToList();
        BulkImageResult result;
        try
        {
            result = await images.BulkAsync(realImages, new BulkImageChange(request.NoPeople, request.CustomExpiry, request.ClearCustomExpiry), Actor);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        foreach (var key in result.Updated) await fileIndex.RefreshAsync(key);
        return Ok(new BulkImageDto(result.Updated.Count, result.Skipped.Count + (keys.Count - realImages.Count)));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(GovernanceOptions), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings() => Ok(WithEffectiveFrontEnd(await settings.GetAsync()));

    [HttpGet("user-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<UserGroupInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUserGroups() => Ok(await groupRecipients.ListGroupsAsync());

    [HttpPut("settings")]
    [ProducesResponseType(typeof(GovernanceOptions), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveSettings([FromBody] GovernanceOptions options)
    {
        try
        {
            var saved = await settings.SaveAsync(options);
            gateCache.Clear();
            await audit.WriteAsync(Actor, "settings.updated",
                detail: $"retentionYears={saved.RetentionYears}, warningDays={saved.WarningDays}, recipients={saved.NotificationEmails.Count}, userGroups={saved.NotificationUserGroupIds?.Count ?? 0}, blockFiles={saved.BlockWithheldFiles?.ToString() ?? "default"}, hidePages={saved.HideWithheldInPages?.ToString() ?? "default"}, signs={saved.ShowStatusSigns ?? true}, treeColours={saved.ColourTreeIcons ?? true}, folderMarkers={saved.ShowFolderMarkers ?? true}");
            return Ok(WithEffectiveFrontEnd(saved));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Fills in the front-end choices still on their default, so the dashboard always shows what is really happening.</summary>
    private GovernanceOptions WithEffectiveFrontEnd(GovernanceOptions saved)
    {
        var policy = FrontEndPolicy.Resolve(saved, appSettings.Value.BlockWithheldMediaFiles);
        return saved with { BlockWithheldFiles = policy.BlockFiles, HideWithheldInPages = policy.HidePages };
    }

    private async Task<ImageGovernanceDto> BuildAsync(Guid mediaKey)
    {
        var (image, tagged) = await images.GetAsync(mediaKey);
        var taggedPeople = await personRepository.GetManyAsync(tagged);
        var governanceOptions = await settings.GetAsync();
        var options = governanceOptions.ToCore();
        var uploaded = (await uploads.GetAsync([mediaKey])).TryGetValue(mediaKey, out var u) ? u : (DateOnly?)null;
        var facts = new ImageGovernanceFacts(
            mediaKey, Dates.FromDb(image.DateTaken), Dates.FromDb(image.DateObtained), Dates.FromDb(image.CustomExpiry),
            image.NoPeople, tagged,
            taggedPeople.Where(p => p.ConsentGiven).Select(p => new ConsentFacts(p.Id, ConsentFacts.Always, null, null)).ToList(),
            uploaded);
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        return new ImageGovernanceDto(
            mediaKey, facts.DateTaken, facts.DateObtained, facts.CustomExpiry, facts.NoPeople, tagged,
            StatusCalculator.EffectiveExpiry(facts, options), StatusCalculator.ExpirySourceOf(facts, options),
            StatusCalculator.Calculate(facts, options, today),
            StatusCalculator.Issues(facts, options, today),
            taggedPeople.OrderBy(p => p.Name).Select(p => new TaggedPersonDto(p.Id, p.Name, p.ConsentGiven)).ToList(), uploaded,
            facts.DateTaken is null ? await photoDates.FindAsync(mediaKey) : null);
    }

}
