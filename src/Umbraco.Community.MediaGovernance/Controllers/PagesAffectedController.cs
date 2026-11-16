using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Controllers;

[ApiController]
[ApiVersion("1.0")]
[MapToApi(Constants.ApiName)]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
[Route($"umbraco/{Constants.ApiName}/api/v{{version:apiVersion}}")]
public class PagesAffectedController(AffectedPagesService pages, IBackOfficeSecurityAccessor security, GovernanceAccessChecker access)
    : GovernanceControllerBase(security, access)
{
    /// <summary>Pages that use expired images or images without consent, from Umbraco's tracked references.</summary>
    [HttpGet("pages-affected")]
    [ProducesResponseType(typeof(IReadOnlyList<AffectedPage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get() => Ok(await pages.GetAsync());
}
