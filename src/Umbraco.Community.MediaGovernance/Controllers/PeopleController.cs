using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
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
public class PeopleController(PersonService people, IImageRepository images, MediaGateCache gateCache, IBackOfficeSecurityAccessor security) : GovernanceControllerBase(security)
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
            return BadRequest(ex.Message);
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
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Permanently erases the person, their tags, consent records and evidence files.</summary>
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
        new(p.Id, p.Name, p.Email, p.Phone, p.LinkedMemberKey, p.LinkedUserKey, p.JobTitle, taggedIn);
}
