using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.MediaGovernance.Services.Settings;

namespace Umbraco.Community.MediaGovernance.Controllers;

public abstract class GovernanceControllerBase : Controller
{
	private readonly IBackOfficeSecurityAccessor _security;
	private readonly GovernanceAccessChecker _access;

	protected GovernanceControllerBase(IBackOfficeSecurityAccessor security, GovernanceAccessChecker access)
	{
		_security = security;
		_access = access;
	}

	/// <summary>Key of the signed-in backoffice user, recorded in audit entries.</summary>
	protected string Actor => _security.BackOfficeSecurity?.CurrentUser?.Key.ToString() ?? "unknown";

	/// <summary>Only members of the configured user groups may use any Governance endpoint.</summary>
	public override void OnActionExecuting(ActionExecutingContext context)
	{
		if (!_access.IsAllowed(_security.BackOfficeSecurity?.CurrentUser))
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
