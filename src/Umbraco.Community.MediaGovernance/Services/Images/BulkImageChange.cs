namespace Umbraco.Community.MediaGovernance.Services.Images;

/// <summary>One change applied to many images: mark them as having no people, set one expiry date, or clear it.</summary>
public record BulkImageChange(bool NoPeople, DateOnly? CustomExpiry, bool ClearCustomExpiry);
