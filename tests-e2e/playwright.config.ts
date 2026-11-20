import { defineConfig } from "@playwright/test";

const url = process.env.MG_URL ?? "https://localhost:44399";

export default defineConfig({
    testDir: "./tests",
    globalSetup: "./global-setup.ts",
    timeout: 45_000,
    expect: { timeout: 10_000 },
    workers: 1, // one shared test site and database
    retries: 0,
    reporter: [["list"], ["html", { open: "never" }]],
    use: {
        baseURL: url,
        ignoreHTTPSErrors: true, // the test site uses the local dev certificate
        channel: "chrome", // the installed Chrome, so no browser download is needed
        screenshot: "only-on-failure",
        trace: "retain-on-failure",
    },
    // Starts the test site if it is not already running. Seeds itself on first start.
    webServer: {
        command: "dotnet run --project ../src/Umbraco.Community.MediaGovernance.TestSite --no-launch-profile --urls https://localhost:44399",
        // Ready once the sample data has been created (not just once the site answers).
        url: `${url}/test-site-ready`,
        ignoreHTTPSErrors: true,
        reuseExistingServer: true,
        timeout: 240_000,
        // A database of its own, so the tests never touch the one used for day-to-day work. It is created and seeded on
        // first start; change the file name here (for example e2e-6) to start again from the seed data.
        env: {
            ASPNETCORE_ENVIRONMENT: "Development",
            ConnectionStrings__umbracoDbDSN: "Data Source=|DataDirectory|/Umbraco.e2e-5.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True",
        },
    },
});
