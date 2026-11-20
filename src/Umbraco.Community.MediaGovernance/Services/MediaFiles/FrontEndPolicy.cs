namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

/// <summary>What the public website does with images that are withheld (Expired, or without consent).</summary>
/// <param name="BlockFiles">Requests for the image file return 404.</param>
/// <param name="HidePages">The &lt;media-governance&gt; tag helper leaves the image out of pages.</param>
public record FrontEndPolicy(bool BlockFiles, bool HidePages);
