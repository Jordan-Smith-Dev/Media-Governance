using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Community.MediaGovernance.Handlers;
using Umbraco.Community.MediaGovernance.Jobs;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services;
using Umbraco.Community.MediaGovernance.Services.AffectedPages;
using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Evidence;
using Umbraco.Community.MediaGovernance.Services.Images;
using Umbraco.Community.MediaGovernance.Services.MediaFiles;
using Umbraco.Community.MediaGovernance.Services.Notifications;
using Umbraco.Community.MediaGovernance.Services.Overview;
using Umbraco.Community.MediaGovernance.Services.People;
using Umbraco.Community.MediaGovernance.Services.Settings;
using Umbraco.Community.MediaGovernance.Services.Status;
using Umbraco.Extensions;

namespace Umbraco.Community.MediaGovernance.Composers;

public class MediaGovernanceComposer : IComposer
{
	public void Compose(IUmbracoBuilder builder)
	{
		var s = builder.Services;

		// The package assembly is not always picked up by MVC's default part discovery, so register it explicitly.
		s.AddMvcCore().AddApplicationPart(typeof(MediaGovernanceComposer).Assembly);

		s.AddScoped<IPersonRepository, PersonRepository>();
		s.AddScoped<IImageRepository, ImageRepository>();
		s.AddScoped<IAuditRepository, AuditRepository>();
		s.AddScoped<INotifiedRepository, NotifiedRepository>();
		s.AddScoped<IMediaFileRepository, MediaFileRepository>();

		s.AddSingleton<GovernanceAccessChecker>();

		// Evidence goes in a folder of its own in Umbraco's media file system (never served), or, if EvidenceStoragePath is
		// set, in a folder on disk outside the website.
		s.AddSingleton<IEvidenceFiles>(sp =>
		{
			var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaGovernanceOptions>>().Value;
			if (!string.IsNullOrWhiteSpace(o.EvidenceStoragePath))
			{
				var path = Path.IsPathRooted(o.EvidenceStoragePath)
					? o.EvidenceStoragePath
					: sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().ContentRootPath is { } root ? Path.Combine(root, o.EvidenceStoragePath) : o.EvidenceStoragePath;
				return new DiskEvidenceFiles(path);
			}
			return new MediaEvidenceFiles(sp.GetRequiredService<Umbraco.Cms.Core.IO.MediaFileManager>(), o.EvidenceFolder);
		});
		s.AddSingleton<IEvidenceStore, EvidenceStore>();
		s.AddScoped<PersonDataExporter>();
		s.AddScoped<IAuditService, AuditService>();
		s.AddScoped<ISettingsService, SettingsService>();
		s.AddScoped<ImageCatalog>();
		s.AddScoped<IImageCatalog>(sp => sp.GetRequiredService<ImageCatalog>());
		s.AddScoped<PersonService>();
		s.AddScoped<ImageGovernanceService>();
		s.AddScoped<OverviewService>();
		s.AddScoped<IMediaUploadDates, EntityMediaUploadDates>();
		s.AddScoped<FolderIssuesService>();
		s.AddScoped<StatusLookupService>();
		s.AddScoped<IMediaReferenceSource, TrackedMediaReferenceSource>();
		s.AddScoped<AffectedPagesService>();
		s.AddScoped<IMediaGovernanceUsage, MediaGovernanceUsage>();
		s.AddHttpContextAccessor();
		s.AddSingleton<IWithheldMediaReporter, WithheldMediaReporter>();

		// Stop withheld media files being served directly (on by default; see MediaGovernanceOptions).
		s.Configure<MediaGovernanceOptions>(builder.Config.GetSection("Umbraco:Community:MediaGovernance"));
		s.AddSingleton<MediaGateCache>();
		s.AddScoped<IMediaFileIndex, MediaFileIndex>();
		s.AddScoped<MediaFileGate>();
		s.AddScoped<IFrontEndPolicyProvider, FrontEndPolicyProvider>();
		s.Configure<UmbracoPipelineOptions>(options =>
			options.AddFilter(new UmbracoPipelineFilter("MediaGovernance")
			{
				// Before Umbraco serves static files, so /media requests reach the gate first.
				PrePipeline = app => app.UseMiddleware<WithheldMediaFileMiddleware>()
			}));
		builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, MediaFileIndexStartupHandler>();
		builder.AddNotificationAsyncHandler<MediaSavedNotification, MediaFileIndexSavedHandler>();
		s.AddScoped<IGroupRecipients, GroupRecipients>();
		s.AddScoped<IPhotoDates, ExifPhotoDates>();
		s.AddScoped<NotificationService>();
		s.TryAddSingleton(TimeProvider.System);

		builder.FlagProviders().Append<MediaGovernanceFlagProvider>();
		builder.AddNotificationAsyncHandler<MediaDeletedNotification, MediaDeletedHandler>();
		builder.Services.AddRecurringBackgroundJob<ExpiryTransitionJob>();
	}
}
