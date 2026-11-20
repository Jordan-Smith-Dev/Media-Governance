using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Jobs;

internal record Transition(Guid MediaKey, GovernanceStatus To);
