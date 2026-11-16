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
public class PeopleController(PersonService people, IImageRepository images, IImageCatalog catalog, StatusLookupService statuses, MediaGateCache gateCache, IBackOfficeSecurityAccessor security) : GovernanceControllerBase(security)
{
    [HttpGet("people")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? q)
    {
        var counts = await images.CountTagsByPersonAsync();
        return Ok((await people.ListAsync(q)).Select(p => ToDto(p, counts.GetValueOrDefault(p.Id))).ToList());
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
    [ProducesResponseType(typeof(IReadOnlyList<PersonImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TaggedImages(Guid id)
    {
        if (await people.GetAsync(id) is null) return NotFound();

        var keys = await images.ListImageKeysForPersonAsync(id);
        if (keys.Count == 0) return Ok(Array.Empty<PersonImageDto>());

        var info = (await catalog.ListImagesAsync()).ToDictionary(i => i.Key);
        var statusByKey = await statuses.GetStatusesAsync(keys);
        return Ok(keys.Where(info.ContainsKey)
            .Select(k => new PersonImageDto(k, info[k].Name, info[k].Folder, statusByKey.GetValueOrDefault(k)))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
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
