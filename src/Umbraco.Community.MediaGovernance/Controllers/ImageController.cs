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
    ConsentService consents,
    ISettingsService settings,
    IAuditService audit,
    IMediaFileIndex fileIndex,
    MediaGateCache gateCache,
    ImageCatalog catalog,
    IMediaUploadDates uploads,
    TimeProvider time,
    Umbraco.Cms.Core.Services.IUserService users,
    IBackOfficeSecurityAccessor security) : GovernanceControllerBase(security)
{
    private const long MaxUploadBytes = EvidenceStore.MaxBytes + 1024;

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

    [HttpPost("image/{mediaKey:guid}/consent")]
    [ProducesResponseType(typeof(ConsentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddConsent(Guid mediaKey, [FromBody] ConsentRequest request)
    {
        if (!catalog.IsImage(mediaKey)) return NotFound();
        try
        {
            var row = await consents.AddAsync(mediaKey,
                new ConsentInput(request.PersonId, request.GivenOn, request.GivenBy, request.PermittedUses, request.ExpiresOn, request.Notes),
                Actor);
            gateCache.Clear();
            return Ok(ToDto(row, null));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("consent/{consentId:guid}/withdraw")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(Guid consentId, [FromBody] WithdrawRequest request)
    {
        try
        {
            await consents.WithdrawAsync(consentId, request.On ?? DateOnly.FromDateTime(time.GetLocalNow().DateTime), Actor);
            gateCache.Clear();
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("consent/{consentId:guid}/evidence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidence(Guid consentId)
    {
        try
        {
            var file = await consents.OpenEvidenceAsync(consentId, Actor);
            if (file is null) return NotFound();

            // Type comes from the stored extension only; nosniff stops browsers second-guessing it.
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "no-store";
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("consent/{consentId:guid}/evidence/history")]
    [ProducesResponseType(typeof(IReadOnlyList<EvidenceEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidenceHistory(Guid consentId)
    {
        try
        {
            var events = await consents.EvidenceHistoryAsync(consentId);
            var names = new Dictionary<string, string>();
            foreach (var key in events.Select(e => e.ActorKey).Distinct())
                names[key] = await ActorNameAsync(key);
            return Ok(events.Select(e => new EvidenceEventDto(e.Action, DateTime.SpecifyKind(e.WhenUtc, DateTimeKind.Utc), names[e.ActorKey])).ToList());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Backoffice user name for an audit actor key; "System" for automatic actions.</summary>
    private async Task<string> ActorNameAsync(string actorKey)
    {
        if (!Guid.TryParse(actorKey, out var key)) return actorKey == Actors.System ? "System" : "Unknown";
        return (await users.GetAsync(key))?.Name ?? "A removed user";
    }

    [HttpDelete("consent/{consentId:guid}/evidence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEvidence(Guid consentId)
    {
        try
        {
            await consents.RemoveEvidenceAsync(consentId, Actor);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("consent/{consentId:guid}/evidence")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadEvidence(Guid consentId, IFormFile file)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            await consents.AttachEvidenceAsync(consentId, file.FileName, stream, Actor);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(GovernanceOptions), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings() => Ok(await settings.GetAsync());

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
                detail: $"retentionYears={saved.RetentionYears}, warningDays={saved.WarningDays}, recipients={saved.NotificationEmails.Count}");
            return Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<ImageGovernanceDto> BuildAsync(Guid mediaKey)
    {
        var (image, tagged) = await images.GetAsync(mediaKey);
        var consentRows = await consents.ListForImageAsync(mediaKey);
        var governanceOptions = await settings.GetAsync();
        var options = governanceOptions.ToCore();
        var uploaded = (await uploads.GetAsync([mediaKey])).TryGetValue(mediaKey, out var u) ? u : (DateOnly?)null;
        var facts = new ImageGovernanceFacts(
            mediaKey, Dates.FromDb(image.DateTaken), Dates.FromDb(image.DateObtained), Dates.FromDb(image.CustomExpiry),
            image.NoPeople, tagged,
            consentRows.Select(c => new ConsentFacts(c.PersonId, Dates.FromDb(c.GivenOn), Dates.FromDb(c.ExpiresOn), Dates.FromDb(c.WithdrawnOn),
                c.PermittedUses.Split(',', StringSplitOptions.RemoveEmptyEntries))).ToList(),
            uploaded);
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var info = await consents.EvidenceInfoAsync(consentRows);

        return new ImageGovernanceDto(
            mediaKey, facts.DateTaken, facts.DateObtained, facts.CustomExpiry, facts.NoPeople, tagged,
            StatusCalculator.EffectiveExpiry(facts, options), StatusCalculator.ExpirySourceOf(facts, options),
            StatusCalculator.Calculate(facts, options, today),
            StatusCalculator.Issues(facts, options, today),
            StatusCalculator.ClearedUses(facts, governanceOptions.PermittedUses, today),
            consentRows.Select(c => ToDto(c, info.GetValueOrDefault(c.Id))).ToList(), uploaded);
    }

    private static ConsentDto ToDto(ConsentRow c, EvidenceInfo? info) =>
        new(c.Id, c.PersonId, Dates.FromDb(c.GivenOn), c.GivenBy,
            c.PermittedUses.Split(',', StringSplitOptions.RemoveEmptyEntries), Dates.FromDb(c.ExpiresOn),
            Dates.FromDb(c.WithdrawnOn), c.EvidencePath is not null, c.Notes, info?.Type, info?.SizeBytes);
}
