using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Controllers;

public abstract class GovernanceControllerBase(IBackOfficeSecurityAccessor security, GovernanceAccessChecker access) : Controller
{
    /// <summary>Key of the signed-in backoffice user, recorded in audit entries.</summary>
    protected string Actor => security.BackOfficeSecurity?.CurrentUser?.Key.ToString() ?? "unknown";

    /// <summary>Only members of the configured user groups may use any Governance endpoint.</summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!access.IsAllowed(security.BackOfficeSecurity?.CurrentUser))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "No access",
                Detail = "Your user group does not have access to Governance. Ask an administrator to add it to the allowed user groups."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }
        base.OnActionExecuting(context);
    }
}
