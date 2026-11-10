using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Community.MediaGovernance.Controllers;

public abstract class GovernanceControllerBase(IBackOfficeSecurityAccessor security) : Controller
{
    /// <summary>Key of the signed-in backoffice user, recorded in audit entries.</summary>
    protected string Actor => security.BackOfficeSecurity?.CurrentUser?.Key.ToString() ?? "unknown";
}
