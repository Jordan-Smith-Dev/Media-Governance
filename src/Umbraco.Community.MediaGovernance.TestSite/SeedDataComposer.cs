using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.MediaGovernance.TestSite;

/// <summary>Seeds the test site with the 12 images and 6 people from the design prototype (test site only; never part of the package).</summary>
public class SeedDataComposer : IComposer
{
	public void Compose(IUmbracoBuilder builder) =>
		builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SeedDataHandler>();
}
