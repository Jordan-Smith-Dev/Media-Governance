namespace Umbraco.Community.MediaGovernance.Persistence.Repositories;

internal static class Dates
{
	public static DateTime ToDb(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
	public static DateTime? ToDb(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);
	public static DateOnly FromDb(DateTime d) => DateOnly.FromDateTime(d);
	public static DateOnly? FromDb(DateTime? d) => d is null ? null : DateOnly.FromDateTime(d.Value);
}
