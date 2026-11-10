using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Core;

public enum FolderAttention
{
    None,

    /// <summary>Contains images that are expiring soon or still need tagging.</summary>
    Warning,

    /// <summary>Contains images that are expired or lack consent, so cannot be used.</summary>
    Withheld
}

/// <summary>Summarises the images inside a folder (including sub-folders) into one marker for the folder.</summary>
public static class FolderRollup
{
    public static FolderAttention Of(IEnumerable<GovernanceStatus> statuses)
    {
        var worst = FolderAttention.None;
        foreach (var status in statuses)
        {
            switch (status)
            {
                case GovernanceStatus.Expired or GovernanceStatus.NoConsent:
                    return FolderAttention.Withheld;
                case GovernanceStatus.ExpiringSoon or GovernanceStatus.NeedsTagging:
                    worst = FolderAttention.Warning;
                    break;
            }
        }
        return worst;
    }
}
