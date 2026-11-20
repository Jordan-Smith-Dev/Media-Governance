using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

internal record GateDecision(bool Allowed, GovernanceStatus? Status = null);
