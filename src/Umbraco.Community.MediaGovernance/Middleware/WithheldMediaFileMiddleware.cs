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

    /// <summary>Status signs on images in the media tree and collection. Off by default, so the media tree looks like a normal Umbraco one.</summary>
    public bool ShowStatusSigns { get; set; } = false;

    /// <summary>Colour image icons in the media tree by status. Off by default.</summary>
    public bool ColourTreeIcons { get; set; } = false;

    /// <summary>Red or amber marker on folders that contain images needing attention. Off by default.</summary>
    public bool ShowFolderMarkers { get; set; } = false;

    /// <summary>
    /// Aliases of the backoffice user groups allowed to use Governance. When empty, only the
    /// <c>sensitiveData</c> ("Sensitive data") group has access.
    /// </summary>
    public string[] AllowedUserGroups { get; set; } = [];

    // ---- Optional consent detail. Off by default: consent is then just a yes or no on the person.
    // Anything recorded while an option was on is kept if it is switched off, and simply hidden and ignored.

    /// <summary>Record the date consent was given (for example the date on a paper form) and who gave it (the person, a parent or guardian, or a legal representative).</summary>
    public bool ConsentDetails { get; set; } = false;

    /// <summary>Let a consent have an end date. Once it passes, the person has no consent in force and their images become Expired.</summary>
    public bool ConsentExpiry { get; set; } = false;

    /// <summary>Let a signed form or email (PDF, PNG or JPG, up to 10 MB) be attached to a person's consent.</summary>
    public bool ConsentEvidence { get; set; } = false;

    /// <summary>
    /// The uses a consent can cover (for example Web, Print, Social). When empty (the default) consent is not recorded per use.
    /// When set, each consent says which uses it covers and each image shows which uses everyone pictured has agreed to.
    /// </summary>
    public string[] PermittedUses { get; set; } = [];

    /// <summary>
    /// Folder, inside Umbraco's media file system, that holds consent evidence. The folder is never served: requests for it
    /// return 404, and files are only reached through the authenticated Governance API. Ignored when <see cref="EvidenceStoragePath"/> is set.
    /// </summary>
    public string EvidenceFolder { get; set; } = "governance-evidence";

    /// <summary>
    /// Optional folder on disk for evidence, outside the website, instead of the media file system. Relative paths are
    /// resolved from the site's content root. Use this when media is stored in a blob store or served by a CDN, where a
    /// folder cannot be kept private.
    /// </summary>
    public string? EvidenceStoragePath { get; set; }
}

/// <summary>Stops withheld media files being served directly, so knowing or finding the URL is not enough.</summary>
public class WithheldMediaFileMiddleware(RequestDelegate next)
{
    private static readonly PathString MediaRoot = new("/media");

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

        await next(context);
    }
}
