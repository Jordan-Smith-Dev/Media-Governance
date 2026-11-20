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
	/// When true, users in <see cref="AdminSettingsUserGroups"/> get an extra "Admin settings" box on the Settings tab that
	/// overrides the options above without a deployment: the consent options, permitted uses, the front-end switches and the media
	/// tree extras. Off by default, so everything is then decided by appsettings alone. Overrides saved while this was on are kept
	/// if it is switched off again, but ignored. Who can use Governance, and where evidence is stored, can never be changed from the CMS.
	/// </summary>
	public bool AllowAdminSettings { get; set; } = false;

	/// <summary>Aliases of the user groups that may change admin settings, when <see cref="AllowAdminSettings"/> is on. When empty, only the <c>admin</c> group can.</summary>
	public string[] AdminSettingsUserGroups { get; set; } = [];

	/// <summary>
	/// Optional folder on disk for evidence, outside the website, instead of the media file system. Relative paths are
	/// resolved from the site's content root. Use this when media is stored in a blob store or served by a CDN, where a
	/// folder cannot be kept private.
	/// </summary>
	public string? EvidenceStoragePath { get; set; }
}
