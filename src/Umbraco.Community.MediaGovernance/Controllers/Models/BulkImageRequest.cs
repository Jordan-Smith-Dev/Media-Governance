namespace Umbraco.Community.MediaGovernance.Controllers.Models;

public record BulkImageRequest(IReadOnlyList<Guid> MediaKeys, bool NoPeople, DateOnly? CustomExpiry, bool ClearCustomExpiry);
