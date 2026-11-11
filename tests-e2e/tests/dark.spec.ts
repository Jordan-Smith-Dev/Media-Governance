import { expect, test, type Page } from "@playwright/test";
import { login, openGovernance } from "./helpers";

const DASHBOARD = "/umbraco/section/media/dashboard/media-governance";

/** Swaps Umbraco's light stylesheet for its dark one, which is what choosing the Dark theme in a user profile does. */
async function goDark(page: Page) {
    const before = await page.evaluate(() => getComputedStyle(document.body).getPropertyValue("--uui-color-surface").trim());
    await page.evaluate(() => {
        const link = document.querySelector<HTMLLinkElement>('link[href$="/css/light.css"]');
        if (link) link.href = link.href.replace("/css/light.css", "/css/dark.css");
    });
    await expect
        .poll(() => page.evaluate(() => getComputedStyle(document.body).getPropertyValue("--uui-color-surface").trim()), { timeout: 10_000 })
        .not.toBe(before);
}

/** A dialog that ignored the theme would stay white. */
async function expectDarkSurface(page: Page, selector: string) {
    const bg = await page.locator(selector).evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg, `${selector} is still white in dark mode`).not.toBe("rgb(255, 255, 255)");
}

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Overview in dark mode", async ({ page }) => {
    await page.goto(DASHBOARD);
    await page.locator("uui-table-row").first().waitFor();
    await goDark(page);
    await page.screenshot({ path: "test-results/dark-overview.png" });
});

test("Image governance view in dark mode", async ({ page }) => {
    await openGovernance(page, "Campus aerial");
    await goDark(page);
    await page.screenshot({ path: "test-results/dark-image.png", fullPage: false });
});

test("Evidence preview and history in dark mode", async ({ page }) => {
    await openGovernance(page, "Graduation 2023");
    await goDark(page);
    await page.locator('uui-button[label="View evidence"]').first().click();
    await page.locator("dialog.preview[open]").waitFor();
    await expectDarkSurface(page, "dialog.preview[open]");
    await page.screenshot({ path: "test-results/dark-preview.png" });
    await page.locator("dialog.preview[open] .preview-head uui-button[label=Close]").click();
    await page.locator("button.link", { hasText: "History" }).first().click();
    await page.locator("dialog.history[open]").waitFor();
    await expectDarkSurface(page, "dialog.history[open]");
    await page.screenshot({ path: "test-results/dark-history.png" });
});
