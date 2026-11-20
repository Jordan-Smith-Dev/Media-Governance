namespace Umbraco.Community.MediaGovernance.TestSite;

/// <summary>Set once the sample data has been created, so the browser tests can wait for it (see /test-site-ready).</summary>
public static class SeedState
{
	public static volatile bool Done;
}
