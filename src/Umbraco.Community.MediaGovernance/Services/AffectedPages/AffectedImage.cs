using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.AffectedPages;

public record AffectedImage(Guid MediaKey, string Name, GovernanceStatus Status);
