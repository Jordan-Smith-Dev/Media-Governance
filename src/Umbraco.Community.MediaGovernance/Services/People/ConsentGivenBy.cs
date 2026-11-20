namespace Umbraco.Community.MediaGovernance.Services.People;

/// <summary>Who gave a consent. A fixed list: free text could record a consent that is not valid.</summary>
internal static class ConsentGivenBy
{
	public const string Self = "Self";
	public const string ParentOrGuardian = "ParentOrGuardian";
	public const string LegalRepresentative = "LegalRepresentative";

	public static readonly IReadOnlyList<string> All = [Self, ParentOrGuardian, LegalRepresentative];
}
