using System.Net.Mail;

namespace Umbraco.Community.MediaGovernance.Services;

public static class GovernanceOptionsValidator
{
    public const int MaxRecipients = 25;
    public const int MaxUserGroups = 25;
    public const int MaxEmailLength = 254;
    public const int MaxPermittedUses = 12;
    public const int MaxUseLength = 30;

    /// <summary>Trims entries, drops blanks and removes duplicates (ignoring case), keeping the first spelling.</summary>
    public static GovernanceOptions Normalize(GovernanceOptions options) => options with
    {
        PermittedUses = Clean(options.PermittedUses),
        NotificationEmails = Clean(options.NotificationEmails),
        NotificationUserGroupIds = (options.NotificationUserGroupIds ?? []).Distinct().ToList()
    };

    private static List<string> Clean(IEnumerable<string> values) =>
        values.Select(v => v?.Trim() ?? string.Empty)
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Throws <see cref="ArgumentException"/> when the options cannot be saved. Call after <see cref="Normalize"/>.</summary>
    public static void Validate(GovernanceOptions options)
    {
        if (options.RetentionYears < 1 || options.RetentionYears > 100)
            throw new ArgumentException("Retention must be between 1 and 100 years.", nameof(options));
        if (options.WarningDays < 0 || options.WarningDays > 3650)
            throw new ArgumentException("Warning window must be between 0 and 3650 days.", nameof(options));

        if (options.PermittedUses.Count == 0)
            throw new ArgumentException("Add at least one permitted use, otherwise consent cannot be recorded.", nameof(options));
        if (options.PermittedUses.Count > MaxPermittedUses)
            throw new ArgumentException($"Use at most {MaxPermittedUses} permitted uses.", nameof(options));
        foreach (var use in options.PermittedUses)
        {
            if (use.Length > MaxUseLength)
                throw new ArgumentException($"'{use}' is too long: permitted uses can be at most {MaxUseLength} characters.", nameof(options));
            if (use.IndexOfAny([',', '\r', '\n']) >= 0)
                throw new ArgumentException($"'{use}' cannot contain a comma or a line break.", nameof(options));
        }

        if (options.NotificationEmails.Count > MaxRecipients)
            throw new ArgumentException($"Use at most {MaxRecipients} recipients.", nameof(options));
        if ((options.NotificationUserGroupIds?.Count ?? 0) > MaxUserGroups)
            throw new ArgumentException($"Choose at most {MaxUserGroups} user groups.", nameof(options));
        foreach (var email in options.NotificationEmails)
        {
            if (email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email.Trim())
                throw new ArgumentException($"'{email}' is not a valid email address.", nameof(options));
        }
    }
}
