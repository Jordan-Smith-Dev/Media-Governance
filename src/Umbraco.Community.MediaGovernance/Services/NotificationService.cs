using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Community.MediaGovernance.Jobs;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Sends one digest email per run listing images that need attention.</summary>
public class NotificationService(
    ISettingsService settings,
    IGroupRecipients groupRecipients,
    IEmailSender email,
    IOptions<GlobalSettings> global,
    IHostingEnvironment hosting,
    ILogger<NotificationService> logger)
{
    /// <summary>Typed-in addresses plus those from user groups, without repeats (ignoring case).</summary>
    public static List<string> Merge(IEnumerable<string> typed, IEnumerable<string> fromGroups) =>
        typed.Concat(fromGroups).Select(e => e.Trim()).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<bool> SendAsync(IReadOnlyList<Transition> transitions, IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, string>? fileNames = null)
    {
        var options = await settings.GetAsync();
        if (transitions.Count == 0 || !email.CanSendRequiredEmail())
            return false;

        var recipients = Merge(options.NotificationEmails, await groupRecipients.EmailsAsync(options.NotificationUserGroupIds ?? []));
        if (recipients.Count == 0)
            return false;

        var body = DigestBuilder.Build(transitions, names, hosting.ApplicationMainUrl, fileNames);
        var from = global.Value.Smtp?.From ?? "noreply@localhost";
        var sent = 0;
        foreach (var to in recipients)
        {
            try
            {
                await email.SendAsync(new EmailMessage(from, to, "Media governance: images need attention", body, true), "MediaGovernance", false, TimeSpan.FromHours(24));   // a digest is stale after a day
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Media governance digest could not be sent to {Recipient}.", to);
            }
        }
        return sent > 0;
    }
}
