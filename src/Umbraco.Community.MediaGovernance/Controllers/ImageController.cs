using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.MediaGovernance.Controllers.Models;
using Umbraco.Community.MediaGovernance.Core;
using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;
using Umbraco.Community.MediaGovernance.Services.Notifications;
using Umbraco.Community.MediaGovernance.Services.People;
using Umbraco.Community.MediaGovernance.Services.Settings;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Controllers;

[ApiController]
[ApiVersion("1.0")]
[MapToApi(Constants.ApiName)]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[ApiExplorerSettings(GroupName = Constants.ApiName)]
[Route($"umbraco/{Constants.ApiName}/api/v{{version:apiVersion}}")]
public class ImageController : GovernanceControllerBase
{
	private readonly ImageGovernanceService _images;
	private readonly IPersonRepository _personRepository;
	private readonly ISettingsService _settings;
	private readonly IAuditService _audit;
	private readonly IMediaFileIndex _fileIndex;
	private readonly MediaGateCache _gateCache;
	private readonly ImageCatalog _catalog;
	private readonly IMediaUploadDates _uploads;
	private readonly TimeProvider _time;
	private readonly IGroupRecipients _groupRecipients;
	private readonly IPhotoDates _photoDates;
	private readonly IBackOfficeSecurityAccessor _security;
	private readonly GovernanceAccessChecker _access;

	public ImageController(
		ImageGovernanceService images,
		IPersonRepository personRepository,
		ISettingsService settings,
		IAuditService audit,
		IMediaFileIndex fileIndex,
		MediaGateCache gateCache,
		ImageCatalog catalog,
		IMediaUploadDates uploads,
		TimeProvider time,
		Umbraco.Cms.Core.Services.IUserService users,
		IGroupRecipients groupRecipients,
		IPhotoDates photoDates,
		IBackOfficeSecurityAccessor security,
		GovernanceAccessChecker access)
		: base(security, access)
	{
		_images = images;
		_personRepository = personRepository;
		_settings = settings;
		_audit = audit;
		_fileIndex = fileIndex;
		_gateCache = gateCache;
		_catalog = catalog;
		_uploads = uploads;
		_time = time;
		_groupRecipients = groupRecipients;
		_photoDates = photoDates;
		_security = security;
		_access = access;
	}

	[HttpGet("image/{mediaKey:guid}")]
	[ProducesResponseType(typeof(ImageGovernanceDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Get(Guid mediaKey)
	{
		if (!_catalog.IsImage(mediaKey))
			return NotFound();
		return Ok(await BuildAsync(mediaKey));
	}

	[HttpPut("image/{mediaKey:guid}")]
	[ProducesResponseType(typeof(ImageGovernanceDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Save(Guid mediaKey, [FromBody] ImageGovernanceRequest request)
	{
		if (!_catalog.IsImage(mediaKey))
			return NotFound();
		try
		{
			await _images.SaveAsync(mediaKey,
				new ImageGovernanceUpdate(request.DateTaken, request.DateObtained, request.CustomExpiry, request.NoPeople, request.TaggedPersonIds),
				Actor);
		}
		catch (ArgumentException ex)
		{
			return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
		}
		await _fileIndex.RefreshAsync(mediaKey);   // also clears the file-request cache
		return Ok(await BuildAsync(mediaKey));
	}

	[HttpPost("images/bulk")]
	[ProducesResponseType(typeof(BulkImageDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Bulk([FromBody] BulkImageRequest request)
	{
		// Only real images are changed; anything else is counted as skipped.
		var keys = (request.MediaKeys ?? []).Distinct().ToList();
		var realImages = keys.Where(_catalog.IsImage).ToList();
		BulkImageResult result;
		try
		{
			result = await _images.BulkAsync(realImages, new BulkImageChange(request.NoPeople, request.CustomExpiry, request.ClearCustomExpiry), Actor);
		}
		catch (ArgumentException ex)
		{
			return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
		}
		foreach (var key in result.Updated)
			await _fileIndex.RefreshAsync(key);
		return Ok(new BulkImageDto(result.Updated.Count, result.Skipped.Count + (keys.Count - realImages.Count)));
	}

	[HttpGet("settings")]
	[ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetSettings() => Ok(ToDto(await _settings.GetAsync()));

	[HttpGet("user-groups")]
	[ProducesResponseType(typeof(IReadOnlyList<UserGroupInfo>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListUserGroups() => Ok(await _groupRecipients.ListGroupsAsync());

	/// <summary>Saves the settings the CMS can change: retention, warning window and recipients. Everything else is appsettings.</summary>
	[HttpPut("settings")]
	[ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> SaveSettings([FromBody] SettingsUpdate update)
	{
		try
		{
			var before = await _settings.GetAsync();
			var saved = await _settings.SaveAsync(update);
			_gateCache.Clear();
			// Old and new values are recorded so a change can be traced. Recipients are counted, not listed: addresses are personal data.
			await _audit.WriteAsync(Actor, "settings.updated", detail:
				$"retentionYears {before.RetentionYears} -> {saved.RetentionYears}; " +
				$"warningDays {before.WarningDays} -> {saved.WarningDays}; " +
				$"recipients {before.NotificationEmails.Count} -> {saved.NotificationEmails.Count}; " +
				$"userGroups {before.NotificationUserGroupIds?.Count ?? 0} -> {saved.NotificationUserGroupIds?.Count ?? 0}");
			return Ok(ToDto(saved));
		}
		catch (ArgumentException ex)
		{
			return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
		}
	}

	/// <summary>
	/// Saves the admin settings: the consent options, permitted uses, the front-end switches and the media tree extras. Only for
	/// admins, and only while AllowAdminSettings is on. Every change is audited with the old and new value.
	/// </summary>
	[HttpPut("settings/admin")]
	[ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> SaveAdminSettings([FromBody] AdminSettingsUpdate update)
	{
		if (!_access.IsAdmin(_security.BackOfficeSecurity?.CurrentUser))
			return Problem(detail: "Only administrators can change these settings, and admin settings must be switched on in appsettings.", statusCode: StatusCodes.Status403Forbidden);

		try
		{
			var before = await _settings.GetAsync();
			var saved = await _settings.SaveAdminAsync(update);
			_gateCache.Clear();
			var changes = AdminChanges(before, saved);
			if (changes.Count > 0)
				await _audit.WriteAsync(Actor, "settings.admin.updated", detail: string.Join("; ", changes));
			return Ok(ToDto(saved));
		}
		catch (ArgumentException ex)
		{
			return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
		}
	}

	/// <summary>The admin settings that changed, as "name old -> new". Permitted use names are not personal data, so they are listed.</summary>
	public static List<string> AdminChanges(GovernanceOptions before, GovernanceOptions after)
	{
		var changes = new List<string>();
		void Flag(string name, bool a, bool b)
		{ if (a != b) changes.Add($"{name} {a} -> {b}"); }
		Flag("blockWithheldFiles", before.BlockWithheldFiles, after.BlockWithheldFiles);
		Flag("hideWithheldInPages", before.HideWithheldInPages, after.HideWithheldInPages);
		Flag("showStatusSigns", before.ShowStatusSigns, after.ShowStatusSigns);
		Flag("colourTreeIcons", before.ColourTreeIcons, after.ColourTreeIcons);
		Flag("showFolderMarkers", before.ShowFolderMarkers, after.ShowFolderMarkers);
		Flag("consentDetails", before.ConsentDetails, after.ConsentDetails);
		Flag("consentExpiry", before.ConsentExpiry, after.ConsentExpiry);
		Flag("consentEvidence", before.ConsentEvidence, after.ConsentEvidence);
		var a = before.PermittedUses ?? [];
		var b = after.PermittedUses ?? [];
		if (!a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase))
			changes.Add($"permittedUses [{string.Join(", ", a)}] -> [{string.Join(", ", b)}]");
		return changes;
	}

	private static AdminValues AdminValuesOf(GovernanceOptions o) => new(
		o.BlockWithheldFiles, o.HideWithheldInPages, o.ShowStatusSigns, o.ColourTreeIcons, o.ShowFolderMarkers,
		o.ConsentDetails, o.ConsentExpiry, o.ConsentEvidence, o.PermittedUses ?? []);

	private static AdminValues AdminDefaultsOf(Umbraco.Community.MediaGovernance.Middleware.MediaGovernanceOptions a) => new(
		a.BlockWithheldMediaFiles, a.HideWithheldInPages, a.ShowStatusSigns, a.ColourTreeIcons, a.ShowFolderMarkers,
		a.ConsentDetails, a.ConsentExpiry, a.ConsentEvidence, AdminSettingsValidator.CleanUses(a.PermittedUses));

	private SettingsDto ToDto(GovernanceOptions o) => new(
		o.RetentionYears, o.WarningDays,
		_settings.AppSettings.RetentionYears, _settings.AppSettings.WarningDays,
		o.NotificationEmails, o.NotificationUserGroupIds ?? [],
		o.BlockWithheldFiles, o.HideWithheldInPages,
		_access.AllowedGroups,
		o.ShowStatusSigns, o.ShowFolderMarkers,
		o.ConsentDetails, o.ConsentExpiry, o.ConsentEvidence, o.PermittedUses ?? [],
		o.ConsentEvidence ? (string.IsNullOrWhiteSpace(_settings.AppSettings.EvidenceStoragePath) ? $"Media folder \"{_settings.AppSettings.EvidenceFolder}\" (never served to the public)" : "A folder on disk set in appsettings") : null,
		_access.IsAdmin(_security.BackOfficeSecurity?.CurrentUser) ? new AdminSettingsDto(AdminValuesOf(o), AdminDefaultsOf(_settings.AppSettings)) : null);

	private async Task<ImageGovernanceDto> BuildAsync(Guid mediaKey)
	{
		var (image, tagged) = await _images.GetAsync(mediaKey);
		var taggedPeople = await _personRepository.GetManyAsync(tagged);
		var governanceOptions = await _settings.GetAsync();
		var options = governanceOptions.ToCore();
		var uploaded = (await _uploads.GetAsync([mediaKey])).TryGetValue(mediaKey, out var u) ? u : (DateOnly?)null;
		var facts = new ImageGovernanceFacts(
			mediaKey, Dates.FromDb(image.DateTaken), Dates.FromDb(image.DateObtained), Dates.FromDb(image.CustomExpiry),
			image.NoPeople, tagged,
			taggedPeople.Select(p => ConsentFactsFactory.Create(p, governanceOptions)).OfType<ConsentFacts>().ToList(),
			uploaded);
		var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

		return new ImageGovernanceDto(
			mediaKey, facts.DateTaken, facts.DateObtained, facts.CustomExpiry, facts.NoPeople, tagged,
			StatusCalculator.EffectiveExpiry(facts, options), StatusCalculator.EffectiveExpiry(facts with { CustomExpiry = null }, options),
			StatusCalculator.ExpirySourceOf(facts, options),
			StatusCalculator.Calculate(facts, options, today),
			StatusCalculator.Issues(facts, options, today),
			taggedPeople.OrderBy(p => p.Name).Select(p => new TaggedPersonDto(p.Id, p.Name,
				ConsentFactsFactory.InForce(p, governanceOptions.ConsentDetails, governanceOptions.ConsentExpiry, today),
				ConsentFactsFactory.Expired(p, governanceOptions.ConsentExpiry, today))).ToList(),
			governanceOptions.UsesOn ? StatusCalculator.ClearedUses(facts, governanceOptions.PermittedUses!, today) : [],
			uploaded,
			facts.DateTaken is null ? await _photoDates.FindAsync(mediaKey) : null);
	}

}
