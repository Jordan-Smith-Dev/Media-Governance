using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Community.MediaGovernance.Jobs;
using Umbraco.Community.MediaGovernance.Services.Settings;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Services.Notifications;

/// <summary>Sends one digest email per run listing images that need attention.</summary>
internal class NotificationService
{
	private readonly ISettingsService _settings;
	private readonly IGroupRecipients _groupRecipients;
	private readonly IEmailSender _email;
	private readonly IOptions<GlobalSettings> _global;
	private readonly IHostingEnvironment _hosting;
	private readonly ILogger<NotificationService> _logger;

	public NotificationService(
		ISettingsService settings,
		IGroupRecipients groupRecipients,
		IEmailSender email,
		IOptions<GlobalSettings> global,
		IHostingEnvironment hosting,
		ILogger<NotificationService> logger)
	{
		_settings = settings;
		_groupRecipients = groupRecipients;
		_email = email;
		_global = global;
		_hosting = hosting;
		_logger = logger;
	}

	/// <summary>Typed-in addresses plus those from user groups, without repeats (ignoring case).</summary>
	public static List<string> Merge(IEnumerable<string> typed, IEnumerable<string> fromGroups) =>
		typed.Concat(fromGroups).Select(e => e.Trim()).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

	public async Task<bool> SendAsync(IReadOnlyList<Transition> transitions, IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, string>? fileNames = null)
	{
		var options = await _settings.GetAsync();
		if (transitions.Count == 0 || !_email.CanSendRequiredEmail())
			return false;

		var recipients = Merge(options.NotificationEmails, await _groupRecipients.EmailsAsync(options.NotificationUserGroupIds ?? []));
		if (recipients.Count == 0)
			return false;

		var body = DigestBuilder.Build(transitions, names, _hosting.ApplicationMainUrl, fileNames);
		var from = _global.Value.Smtp?.From ?? "noreply@localhost";
		var sent = 0;
		foreach (var to in recipients)
		{
			try
			{
				await _email.SendAsync(new EmailMessage(from, to, "Media governance: images need attention", body, true), "MediaGovernance", false, TimeSpan.FromHours(24));   // a digest is stale after a day
				sent++;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Media governance digest could not be sent to {Recipient}.", to);
			}
		}
		return sent > 0;
	}
}
