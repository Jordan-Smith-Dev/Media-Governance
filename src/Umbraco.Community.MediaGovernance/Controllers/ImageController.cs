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
    GovernanceAccessChecker access) : GovernanceControllerBase(security, access)
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
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings() => Ok(ToDto(await settings.GetAsync()));

    [HttpGet("user-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<UserGroupInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUserGroups() => Ok(await groupRecipients.ListGroupsAsync());

    /// <summary>Saves the settings the CMS can change: retention, warning window and recipients. Everything else is appsettings.</summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveSettings([FromBody] SettingsUpdate update)
    {
        try
        {
            var before = await settings.GetAsync();
            var saved = await settings.SaveAsync(update);
            gateCache.Clear();
            // Old and new values are recorded so a change can be traced. Recipients are counted, not listed: addresses are personal data.
            await audit.WriteAsync(Actor, "settings.updated", detail:
                $"retentionYears {before.RetentionYears} -> {saved.RetentionYears}; " +
                $"warningDays {before.WarningDays} -> {saved.WarningDays}; " +
                $"recipients {before.NotificationEmails.Count} -> {saved.NotificationEmails.Count}; " +
                $"userGroups {before.NotificationUserGroupIds?.Count ?? 0} -> {saved.NotificationUserGroupIds?.Count ?? 0}");
            return Ok(ToDto(saved));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private SettingsDto ToDto(GovernanceOptions o) => new(
        o.RetentionYears, o.WarningDays,
        settings.AppSettings.RetentionYears, settings.AppSettings.WarningDays,
        o.NotificationEmails, o.NotificationUserGroupIds ?? [],
        o.BlockWithheldFiles, o.HideWithheldInPages,
        access.AllowedGroups);

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
