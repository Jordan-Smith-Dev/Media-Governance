using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
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
public class PeopleController(PersonService people, IImageRepository images, IImageCatalog catalog, StatusLookupService statuses, MediaGateCache gateCache, IBackOfficeSecurityAccessor security, GovernanceAccessChecker access) : GovernanceControllerBase(security, access)
{
    /// <summary>One page of people, matching the search text if given. Newest pages are fetched with skip and take.</summary>
    [HttpGet("people")]
    [ProducesResponseType(typeof(PeoplePage), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var counts = await images.CountTagsByPersonAsync();
        var all = await people.ListAsync(q);
        var page = all.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
            .Select(p => ToDto(p, counts.GetValueOrDefault(p.Id))).ToList();
        return Ok(new PeoplePage(page, all.Count));
    }

    [HttpPost("people")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] PersonRequest request)
    {
        try
        {
            return Ok(ToDto(await people.CreateAsync(ToInput(request), Actor), 0));
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
            return Ok(ToDto(updated, (await images.CountTagsByPersonAsync()).GetValueOrDefault(id)));
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

    /// <summary>Sets whether the person has given consent. Changes the status of every image they are tagged in.</summary>
    [HttpPut("people/{id:guid}/consent")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetConsent(Guid id, [FromBody] ConsentChangeRequest request)
    {
        try
        {
            var updated = await people.SetConsentAsync(id, request.Given, Actor);
            gateCache.Clear();
            return Ok(ToDto(updated, (await images.CountTagsByPersonAsync()).GetValueOrDefault(id)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

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

    /// <summary>Permanently erases the person and their image tags.</summary>
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

    private static PersonInput ToInput(PersonRequest r) => new(r.Name, r.Email, r.Phone, r.LinkedMemberKey, r.LinkedUserKey, r.JobTitle);

    private static PersonDto ToDto(PersonRow p, int taggedIn) =>
        new(p.Id, p.Name, p.Email, p.Phone, p.LinkedMemberKey, p.LinkedUserKey, p.JobTitle, taggedIn, p.ConsentGiven, p.ConsentUpdatedUtc);
}
