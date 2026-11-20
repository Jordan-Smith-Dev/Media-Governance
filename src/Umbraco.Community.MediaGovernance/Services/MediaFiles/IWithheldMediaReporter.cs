using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

/// <summary>Records that a page asked to show media that governance has withheld, so it can be removed or replaced.</summary>
public interface IWithheldMediaReporter
{
	void Report(Guid mediaKey, GovernanceStatus status, string? pagePath);
}
