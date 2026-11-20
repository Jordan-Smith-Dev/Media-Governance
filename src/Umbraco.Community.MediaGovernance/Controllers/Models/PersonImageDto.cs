using Umbraco.Community.MediaGovernance.Core.Models;

namespace Umbraco.Community.MediaGovernance.Controllers.Models;

internal record PersonImageDto(Guid MediaKey, string Name, string Folder, GovernanceStatus Status);
