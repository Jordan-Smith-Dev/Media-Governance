namespace Umbraco.Community.MediaGovernance.Services.Images;

/// <summary>The date a photo says it was taken, according to its embedded camera data.</summary>
public interface IPhotoDates
{
	Task<DateOnly?> FindAsync(Guid mediaKey);
}
