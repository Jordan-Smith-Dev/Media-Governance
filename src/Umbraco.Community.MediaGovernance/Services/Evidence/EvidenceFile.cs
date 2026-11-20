namespace Umbraco.Community.MediaGovernance.Services.Evidence;

/// <summary>An evidence file ready to stream. The caller disposes <see cref="Content"/>.</summary>
public sealed record EvidenceFile(Stream Content, string ContentType, string FileName);
