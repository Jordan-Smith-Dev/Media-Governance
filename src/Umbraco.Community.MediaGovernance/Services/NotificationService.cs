using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Community.MediaGovernance.Jobs;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>Sends one digest email per run listing images that need attention.</summary>
public class NotificationService(
    ISettingsService settings,
    IEmailSender email,
    IOptions<GlobalSettings> global,
    ILogger<NotificationService> logger)
{
    public async Task<bool> SendAsync(IReadOnlyList<Transition> transitions, IReadOnlyDictionary<Guid, string> names)
    {
        var options = await settings.GetAsync();
        if (transitions.Count == 0 || options.NotificationEmails.Count == 0 || !email.CanSendRequiredEmail())
            return false;

        var body = DigestBuilder.Build(transitions, names);
        var from = global.Value.Smtp?.From ?? "noreply@localhost";
        var sent = 0;
        foreach (var to in options.NotificationEmails)
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
