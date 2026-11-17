using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
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
public class PeopleController(
    PersonService people,
    IImageRepository images,
    IImageCatalog catalog,
    StatusLookupService statuses,
    ISettingsService settings,
    PersonDataExporter exporter,
    IUserService users,
    TimeProvider time,
    MediaGateCache gateCache,
    IBackOfficeSecurityAccessor security,
    GovernanceAccessChecker access) : GovernanceControllerBase(security, access)
{
    private const long MaxUploadBytes = EvidenceContent.MaxBytes + 1024;

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>One page of people, matching the search text if given. Later pages are fetched with skip and take.</summary>
    [HttpGet("people")]
    [ProducesResponseType(typeof(PeoplePage), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var counts = await images.CountTagsByPersonAsync();
        var all = await people.ListAsync(q);
        var options = await settings.GetAsync();
        var page = new List<PersonDto>();
        foreach (var p in all.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)))
            page.Add(await ToDtoAsync(p, counts.GetValueOrDefault(p.Id), options, includeSize: false));
        return Ok(new PeoplePage(page, all.Count));
    }

    [HttpPost("people")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] PersonRequest request)
    {
        try
        {
            return Ok(await ToDtoAsync(await people.CreateAsync(ToInput(request), Actor), 0));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("people/{id:guid}")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PersonRequest request)
    {
        try
        {
            var updated = await people.UpdateAsync(id, ToInput(request), Actor);
            return Ok(await ToDtoAsync(updated, await TagCountAsync(id)));
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

    /// <summary>Sets whether the person has given consent, with the optional detail that is switched on. Changes the status of every image they are tagged in.</summary>
    [HttpPut("people/{id:guid}/consent")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetConsent(Guid id, [FromBody] ConsentChangeRequest request)
    {
        try
        {
            var updated = await people.SetConsentAsync(id, new ConsentChange(request.Given, request.GivenOn, request.GivenBy, request.ExpiresOn, request.PermittedUses), Actor);
            gateCache.Clear();
            return Ok(await ToDtoAsync(updated, await TagCountAsync(id)));
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

    // ---------------------------------------------------------------- evidence (ConsentEvidence option)

    /// <summary>Streams the evidence file with the backoffice credentials. Each view is audited.</summary>
    [HttpGet("people/{id:guid}/evidence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidence(Guid id)
    {
        try
        {
            var file = await people.OpenEvidenceAsync(id, Actor);
            if (file is null) return NotFound();
            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("people/{id:guid}/evidence/history")]
    [ProducesResponseType(typeof(IReadOnlyList<EvidenceEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidenceHistory(Guid id)
    {
        try
        {
            var events = await people.EvidenceHistoryAsync(id);
            var names = new Dictionary<string, string>();
            var result = new List<EvidenceEventDto>();
            foreach (var e in events)
            {
                if (!names.TryGetValue(e.ActorKey, out var name))
                    names[e.ActorKey] = name = await ActorNameAsync(e.ActorKey);
                result.Add(new EvidenceEventDto(e.Action, e.WhenUtc, name));
            }
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("people/{id:guid}/evidence")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadEvidence(Guid id, IFormFile file)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var updated = await people.AttachEvidenceAsync(id, file.FileName, stream, Actor);
            return Ok(await ToDtoAsync(updated, await TagCountAsync(id)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("people/{id:guid}/evidence")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEvidence(Guid id)
    {
        try
        {
            await people.RemoveEvidenceAsync(id, Actor);
            return Ok(await ToDtoAsync((await people.GetAsync(id))!, await TagCountAsync(id)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ---------------------------------------------------------------- the person's images, export, deletion

    /// <summary>The images this person is tagged in, with each one's current status.</summary>
    [HttpGet("people/{id:guid}/images")]
    [ProducesResponseType(typeof(PersonImagesPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TaggedImages(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 25)
    {
        if (await people.GetAsync(id) is null) return NotFound();

        var keys = await images.ListImageKeysForPersonAsync(id);
        if (keys.Count == 0) return Ok(new PersonImagesPage([], 0));

        var info = (await catalog.ListImagesAsync()).ToDictionary(i => i.Key);
        var known = keys.Where(info.ContainsKey).OrderBy(k => info[k].Name, StringComparer.OrdinalIgnoreCase).ToList();
        // Statuses are worked out only for the page being shown.
        var pageKeys = known.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100)).ToList();
        var statusByKey = pageKeys.Count == 0 ? new Dictionary<Guid, GovernanceStatus>() : (await statuses.GetStatusesAsync(pageKeys)).ToDictionary(k => k.Key, k => k.Value);
        return Ok(new PersonImagesPage(
            pageKeys.Select(k => new PersonImageDto(k, info[k].Name, info[k].Folder, statusByKey.GetValueOrDefault(k))).ToList(),
            known.Count));
    }

    /// <summary>Everything held about the person, as a zip (a JSON summary plus any evidence file), for a subject access request.</summary>
    [HttpGet("people/{id:guid}/export")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPerson(Guid id)
    {
        var built = await exporter.BuildAsync(id, Actor);
        if (built is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(built.Value.Zip, "application/zip", built.Value.FileName);
    }

    /// <summary>Permanently deletes the person, their image tags and any evidence files.</summary>
    [HttpDelete("people/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Erase(Guid id)
    {
        try
        {
            await people.EraseAsync(id, Actor);
            gateCache.Clear();
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<int> TagCountAsync(Guid id) => (await images.CountTagsByPersonAsync()).GetValueOrDefault(id);

    private async Task<string> ActorNameAsync(string key)
    {
        if (!Guid.TryParse(key, out var guid)) return key;
        try
        {
            return (await users.GetAsync(guid))?.Name ?? key;
        }
        catch
        {
            return key;
        }
    }

    private static PersonInput ToInput(PersonRequest r) => new(r.Name, r.Email, r.Phone, r.LinkedMemberKey, r.LinkedUserKey, r.JobTitle);

    /// <summary>The person as the screens see it. Optional detail is only included while its option is switched on (otherwise it is kept but hidden).</summary>
    private async Task<PersonDto> ToDtoAsync(PersonRow p, int taggedIn, GovernanceOptions? options = null, bool includeSize = true)
    {
        var o = options ?? await settings.GetAsync();
        var today = Today;
        string? evidenceType = null;
        long? size = null;
        var hasEvidence = o.ConsentEvidence && p.ConsentEvidencePath is not null;
        if (hasEvidence)
        {
            evidenceType = EvidenceContent.TypeLabel(p.ConsentEvidencePath!);
            if (includeSize) size = (await people.EvidenceInfoAsync(p))?.SizeBytes;
        }

        return new PersonDto(
            p.Id, p.Name, p.Email, p.Phone, p.LinkedMemberKey, p.LinkedUserKey, p.JobTitle, taggedIn,
            p.ConsentGiven, p.ConsentUpdatedUtc,
            ConsentFactsFactory.InForce(p, o.ConsentDetails, o.ConsentExpiry, today),
            ConsentFactsFactory.Expired(p, o.ConsentExpiry, today),
            o.ConsentDetails && p.ConsentGivenOn is { } g ? DateOnly.FromDateTime(g) : null,
            o.ConsentDetails ? p.ConsentGivenBy : null,
            o.ConsentExpiry && p.ConsentExpiresOn is { } x ? DateOnly.FromDateTime(x) : null,
            o.UsesOn ? ConsentFactsFactory.Split(p.ConsentPermittedUses) : null,
            hasEvidence, evidenceType, size);
    }
}
