namespace Umbraco.Community.MediaGovernance.Services.MediaFiles;

public interface IFrontEndPolicyProvider
{
	Task<FrontEndPolicy> GetAsync();
}
