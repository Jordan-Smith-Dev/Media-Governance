using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Middleware;

/// <summary>
/// Settings from appsettings, section <c>Umbraco:Community:MediaGovernance</c>. These are the starting point for everything.
/// In the CMS only the retention period, the warning window and the notification recipients can be changed
/// (retention and the warning window override these values); everything else is read-only there.
/// </summary>
public class MediaGovernanceOptions
{
    /// <summary>How many years an image is kept, counted from the date taken (or obtained, or uploaded). The CMS can override this.</summary>
    public int RetentionYears { get; set; } = 3;

    /// <summary>How many days before an expiry an image shows as "expiring soon". The CMS can override this.</summary>
    public int WarningDays { get; set; } = 90;

    /// <summary>When true (the default), requests for the file of an expired image, or of someone who has not consented, return 404 for everyone except signed-in backoffice users.</summary>
    public bool BlockWithheldMediaFiles { get; set; } = true;

    /// <summary>When true (the default), the &lt;media-governance&gt; tag helper leaves withheld images out of pages.</summary>
    public bool HideWithheldInPages { get; set; } = true;

    /// <summary>Status signs on images in the media tree and collection.</summary>
    public bool ShowStatusSigns { get; set; } = true;

    /// <summary>Colour image icons in the media tree by status.</summary>
    public bool ColourTreeIcons { get; set; } = true;

    /// <summary>Red or amber marker on folders that contain images needing attention.</summary>
    public bool ShowFolderMarkers { get; set; } = true;

    /// <summary>
    /// Aliases of the backoffice user groups allowed to use Governance. When empty, only the
    /// <c>sensitiveData</c> ("Sensitive data") group has access.
    /// </summary>
    public string[] AllowedUserGroups { get; set; } = [];
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
