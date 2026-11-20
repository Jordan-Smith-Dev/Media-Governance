namespace Umbraco.Community.MediaGovernance.Tests;

internal class FixedTimeProvider : TimeProvider
{
	private readonly DateOnly _today;

	public FixedTimeProvider(DateOnly today)
	{
		_today = today;
	}

	public override DateTimeOffset GetUtcNow() => new(_today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
	public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
