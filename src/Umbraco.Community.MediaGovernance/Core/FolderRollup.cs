using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Core;

/// <summary>Summarises the images inside a folder (including sub-folders) into one marker for the folder.</summary>
internal static class FolderRollup
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
