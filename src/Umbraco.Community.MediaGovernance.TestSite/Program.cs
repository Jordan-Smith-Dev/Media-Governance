using Umbraco.Community.MediaGovernance.TestSite;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        // Test site only: 200 once the sample data exists, so the browser tests do not start on a half-seeded site.
        u.EndpointRouteBuilder.MapGet("/test-site-ready", () => SeedState.Done ? Results.Ok() : Results.StatusCode(503));

        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
