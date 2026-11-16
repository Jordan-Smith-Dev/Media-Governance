import { expect, test, type Page } from "@playwright/test";
import { login, openGovernance, view } from "./helpers";

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

/** Text that ignored the theme would stay dark on a dark page. */
async function expectReadable(page: Page, selector: string) {
    const [text, bg] = await page.locator(selector).first().evaluate((el) => {
        const s = getComputedStyle(el);
        return [s.color, s.backgroundColor];
    });
    expect(text, `${selector} text is as dark as its background in dark mode`).not.toBe(bg);
}

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Overview in dark mode", async ({ page }) => {
    await page.goto(view("overview"));
    await page.locator("uui-table-row").first().waitFor();
    await goDark(page);
    await page.screenshot({ path: "test-results/dark-overview.png" });
    await expectReadable(page, "umb-media-governance-overview .name");
});

test("Image governance view in dark mode", async ({ page }) => {
    await openGovernance(page, "Lecture theatre");
    await goDark(page);
    await page.screenshot({ path: "test-results/dark-image.png" });
    // The consent tag keeps its own colours on the dark page.
    const bg = await page.locator("umb-media-governance-image-view uui-tag.person").first().evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg).not.toBe("rgba(0, 0, 0, 0)");
});

test("People and the person modal in dark mode", async ({ page }) => {
    await page.goto(view("people"));
    await page.locator("uui-table-row").first().waitFor();
    await goDark(page);
    await page.screenshot({ path: "test-results/dark-people.png" });
    await page.getByRole("button", { name: "Tom Reyes" }).first().click();
    await page.locator("umb-media-governance-person-modal").waitFor();
    await page.screenshot({ path: "test-results/dark-person-modal.png" });
    await expectReadable(page, "umb-media-governance-person-modal .muted");
});

test("Settings in dark mode", async ({ page }) => {
    await page.goto(view("settings"));
    await page.locator("umb-media-governance-settings uui-input").first().waitFor();
    await goDark(page);
    await page.screenshot({ path: "test-results/dark-settings.png" });
    await expectReadable(page, "umb-media-governance-settings .muted");
    // The retention warning keeps a visible background in dark mode.
    await page.locator('umb-media-governance-settings uui-input[label="Retention in years"] input').fill("9");
    const callout = page.locator("umb-media-governance-settings .callout");
    await expect(callout).toBeVisible();
    const bg = await callout.evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg).not.toBe("rgb(255, 255, 255)");
});
