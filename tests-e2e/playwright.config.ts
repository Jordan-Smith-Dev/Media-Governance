import { defineConfig } from "@playwright/test";

const url = process.env.MG_URL ?? "https://localhost:44399";

export default defineConfig({
    testDir: "./tests",
    timeout: 45_000,
    expect: { timeout: 10_000 },
    workers: 1, // one shared test site and database
    retries: 1, // the backoffice occasionally drops a session mid-test (token refresh), which a retry absorbs
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
        url: `${url}/umbraco/management/api/v1/server/status`,
        ignoreHTTPSErrors: true,
        reuseExistingServer: true,
        timeout: 240_000,
        env: { ASPNETCORE_ENVIRONMENT: "Development" },
    },
});
