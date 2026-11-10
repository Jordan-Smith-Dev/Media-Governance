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
    /// Set <c>Umbraco:Community:MediaGovernance:BlockWithheldMediaFiles</c> to false to turn this off.
    /// </summary>
    public bool BlockWithheldMediaFiles { get; set; } = true;
}

/// <summary>Stops withheld media files being served directly, so knowing or finding the URL is not enough.</summary>
public class WithheldMediaFileMiddleware(RequestDelegate next, IOptions<MediaGovernanceOptions> options)
{
    private static readonly PathString MediaRoot = new("/media");

    public async Task InvokeAsync(HttpContext context, MediaFileGate gate)
    {
        var request = context.Request;
        var isFileRequest = options.Value.BlockWithheldMediaFiles
            && (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && request.Path.StartsWithSegments(MediaRoot);

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
