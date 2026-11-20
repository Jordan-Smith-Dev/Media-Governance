namespace Umbraco.Community.MediaGovernance.Services.People;

/// <summary>One thing that happened to a person's evidence. The actor is a raw key; resolve it to a name for display.</summary>
public sealed record EvidenceEvent(string Action, DateTime WhenUtc, string ActorKey);
