using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Middleware;

public class MediaGovernanceOptions
{
    /// <summary>
    /// When true (the default), requests for the file of an expired image, or an image of someone who has not
    /// consented, return 404 for everyone except signed-in backoffice users.
    /// This is the starting value. Once someone saves the front-end choice in the dashboard's Settings tab, that wins.
    /// Set <c>Umbraco:Community:MediaGovernance:BlockWithheldMediaFiles</c> to false to start with it off.
    /// </summary>
    public bool BlockWithheldMediaFiles { get; set; } = true;
}

/// <summary>Stops withheld media files being served directly, so knowing or finding the URL is not enough.</summary>
public class WithheldMediaFileMiddleware(RequestDelegate next)
{
    private static readonly PathString MediaRoot = new("/media");

    public async Task InvokeAsync(HttpContext context, MediaFileGate gate, IFrontEndPolicyProvider policy)
    {
        var request = context.Request;
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

        await next(context);
    }
}
