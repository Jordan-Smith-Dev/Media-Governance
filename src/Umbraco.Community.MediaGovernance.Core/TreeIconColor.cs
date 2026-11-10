using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Core;

/// <summary>Colours for an image's icon in the media tree. Names are Umbraco icon colour aliases.</summary>
public static class TreeIconColor
{
    /// <summary>A dark amber that stays readable on white (Umbraco's "yellow" is too pale for small icons).</summary>
    public const string Amber = "#b45309";

    /// <summary>The colour for a status, or null to keep the normal icon colour.</summary>
    public static string? For(GovernanceStatus status) => status switch
    {
        GovernanceStatus.Expired => "red",
        GovernanceStatus.NoConsent => "red",
        GovernanceStatus.ExpiringSoon => Amber,
        GovernanceStatus.Compliant => "green",
        _ => null
    };

    /// <summary>Returns the icon with the colour applied (<c>icon-picture color-red</c>), replacing any colour it already had.</summary>
    public static string? Apply(string? icon, string? color)
    {
        if (string.IsNullOrWhiteSpace(icon) || color is null) return icon;
        var name = icon.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return $"{name} color-{color}";
    }
}
