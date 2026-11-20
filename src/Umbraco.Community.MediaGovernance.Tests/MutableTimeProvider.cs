namespace Umbraco.Community.MediaGovernance.Tests;

internal class MutableTimeProvider : TimeProvider
{
	public MutableTimeProvider(DateTimeOffset start)
	{
		Now = start;
	}

	public DateTimeOffset Now { get; set; }
	public override DateTimeOffset GetUtcNow() => Now;
}
