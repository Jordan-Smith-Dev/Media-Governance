using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>
/// Lets templates and code on the public site ask whether an image may be used. Returns only a status or
/// a yes/no, never names or consent details, so it is safe to call while rendering public pages.
/// </summary>
public interface IMediaGovernanceUsage
{
	/// <summary>The image's governance status. Media with no governance data reports <see cref="GovernanceStatus.NeedsTagging"/>.</summary>
	Task<GovernanceStatus> GetStatusAsync(Guid mediaKey);

	/// <summary>False when the image has expired or a tagged person has not consented (see <see cref="UsagePolicy"/>).</summary>
	Task<bool> IsUsableAsync(Guid mediaKey);

	/// <summary>Usability for many images in one lookup. Prefer this when rendering a gallery.</summary>
	Task<IReadOnlyDictionary<Guid, bool>> GetUsableAsync(IReadOnlyCollection<Guid> mediaKeys);
}
