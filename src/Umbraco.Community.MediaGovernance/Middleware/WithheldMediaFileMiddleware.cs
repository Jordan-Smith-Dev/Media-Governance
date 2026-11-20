using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;

namespace Umbraco.Community.MediaGovernance.Middleware;

/// <summary>Stops withheld media files being served directly, so knowing or finding the URL is not enough.</summary>
internal class WithheldMediaFileMiddleware
{
	private static readonly PathString MediaRoot = new("/media");

	private readonly RequestDelegate _next;

	public WithheldMediaFileMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context, MediaFileGate gate, IFrontEndPolicyProvider policy, IOptions<MediaGovernanceOptions> options)
	{
		var request = context.Request;

		// Consent evidence can sit in the media file system. That folder is never served, to anyone: files are only
		// reached through the authenticated Governance API. 404, not 403, so its existence is not confirmed either.
		var evidenceFolder = options.Value.EvidenceFolder?.Trim('/');
		if (!string.IsNullOrEmpty(evidenceFolder) && request.Path.StartsWithSegments(MediaRoot.Add("/" + evidenceFolder)))
		{
			context.Response.StatusCode = StatusCodes.Status404NotFound;
			context.Response.Headers.CacheControl = "no-store";
			return;
		}

		// Cheap checks first, so only requests for media files ever look up the setting.
		var isFileRequest = (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
			&& request.Path.StartsWithSegments(MediaRoot)
			&& (await policy.GetAsync()).BlockFiles;

		if (isFileRequest)
		{
			var backoffice = (await context.AuthenticateAsync(Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType)).Succeeded;
			var decision = await gate.CheckAsync(request.Path.Value ?? string.Empty, backoffice);
			if (!decision.Allowed)
			{
				// 404, not 403: do not confirm that a file exists at this address.
				context.Response.StatusCode = StatusCodes.Status404NotFound;
				context.Response.Headers.CacheControl = "no-store";
				return;
			}
		}

		await _next(context);
	}
}
