using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Controllers;

[ApiController]
[ApiVersion("1.0")]
[MapToApi(Constants.ApiName)]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
[Route($"umbraco/{Constants.ApiName}/api/v{{version:apiVersion}}")]
public class OverviewController(OverviewService overview, IBackOfficeSecurityAccessor security, GovernanceAccessChecker access)
    : GovernanceControllerBase(security, access)
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(OverviewPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] GovernanceStatus? status, [FromQuery] string? filter,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        [FromQuery] OverviewSort sort = OverviewSort.Name, [FromQuery] bool desc = false) =>
        Ok(await overview.QueryAsync(new OverviewQuery(status, filter, skip, Math.Clamp(take, 1, 500), null, sort, desc)));

    [HttpGet("overview/export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export([FromQuery] GovernanceStatus? status, [FromQuery] string? filter, [FromQuery] string? keys = null,
        [FromQuery] OverviewSort sort = OverviewSort.Name, [FromQuery] bool desc = false)
    {
        // keys: comma-separated media keys, used to export just the rows someone ticked
        var chosen = string.IsNullOrWhiteSpace(keys)
            ? null
            : keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => Guid.TryParse(k, out var g) ? g : (Guid?)null)
                .Where(g => g is not null).Select(g => g!.Value).ToList();
        var items = await overview.AllItemsAsync(new OverviewQuery(status, filter, 0, int.MaxValue, chosen, sort, desc));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ExportService.ToCsv(items))).ToArray(),
            "text/csv", "media-governance.csv");
    }

    /// <summary>Status per image, used for the media tree indicators.</summary>
    [HttpGet("image/status-map")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<Guid, GovernanceStatus>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StatusMap() => Ok(await overview.StatusMapAsync());
}
