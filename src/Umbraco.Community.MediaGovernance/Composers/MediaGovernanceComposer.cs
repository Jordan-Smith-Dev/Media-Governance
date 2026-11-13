using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Extensions;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.MediaGovernance.Handlers;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Community.MediaGovernance.Jobs;
using Umbraco.Community.MediaGovernance.Middleware;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Composers
{
    public class MediaGovernanceComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            var s = builder.Services;

            // The package assembly is not always picked up by MVC's default part discovery, so register it explicitly.
            s.AddMvcCore().AddApplicationPart(typeof(MediaGovernanceComposer).Assembly);

            s.AddScoped<IPersonRepository, PersonRepository>();
            s.AddScoped<IImageRepository, ImageRepository>();
            s.AddScoped<IConsentRepository, ConsentRepository>();
            s.AddScoped<IAuditRepository, AuditRepository>();
            s.AddScoped<INotifiedRepository, NotifiedRepository>();
            s.AddScoped<IMediaFileRepository, MediaFileRepository>();

            // Evidence lives under umbraco/Data (outside wwwroot) so it is never served statically.
            s.AddSingleton<IEvidenceStore>(sp => new EvidenceStore(
                sp.GetRequiredService<IHostingEnvironment>().MapPathContentRoot("~/umbraco/Data/MediaGovernance/evidence")));

            s.AddScoped<IAuditService, AuditService>();
            s.AddScoped<ISettingsService, SettingsService>();
            s.AddScoped<ImageCatalog>();
            s.AddScoped<IImageCatalog>(sp => sp.GetRequiredService<ImageCatalog>());
            s.AddScoped<PersonService>();
            s.AddScoped<ConsentService>();
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
            s.AddScoped<NotificationService>();
            s.TryAddSingleton(TimeProvider.System);

            builder.FlagProviders().Append<MediaGovernanceFlagProvider>();
            builder.AddNotificationAsyncHandler<MediaDeletedNotification, MediaDeletedHandler>();
            builder.Services.AddRecurringBackgroundJob<ExpiryTransitionJob>();
        }
    }
}
