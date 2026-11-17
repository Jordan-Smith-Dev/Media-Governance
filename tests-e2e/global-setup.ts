import { chromium, type FullConfig } from "@playwright/test";

/**
 * Wakes the backoffice up once before the tests: a site that has only just started is slow to serve its first page
 * (compiling, loading the backoffice), which made the first test of a run time out. Failing here does not fail the run.
 */
export default async function globalSetup(config: FullConfig) {
    const baseURL = config.projects[0].use.baseURL ?? "https://localhost:44399";
    const browser = await chromium.launch({ channel: "chrome" });
    try {
        const page = await (await browser.newContext({ ignoreHTTPSErrors: true })).newPage();
        page.setDefaultTimeout(120_000);
        await page.goto(`${baseURL}/umbraco`);
        await page.getByLabel(/e-?mail/i).first().fill(process.env.MG_EMAIL ?? "admin@example.com");
        await page.locator("#password-input").fill(process.env.MG_PASSWORD ?? "1234567890");
        await page.keyboard.press("Enter");
        await page.waitForURL(/\/umbraco\/section\//);
        await page.goto(`${baseURL}/umbraco/section/media/workspace/media-governance/view/overview`);
        await page.locator("uui-table-row").first().waitFor();
    } catch {
        // the tests themselves will report any real problem
    } finally {
        await browser.close();
    }
}
