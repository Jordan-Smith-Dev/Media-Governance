import { expect, test } from "@playwright/test";
import { api, login } from "./helpers";

const DASHBOARD = "/umbraco/section/media/dashboard/media-governance";

type Settings = { retentionYears: number; warningDays: number; permittedUses: string[]; notificationEmails: string[]; blockWithheldFiles: boolean; hideWithheldInPages: boolean };

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Front-end choices show their current values, save, and survive a reload", async ({ page }) => {
    const original = await api<Settings>(page, "GET", "settings");
    try {
        await page.goto(DASHBOARD);
        await page.locator('umb-media-governance-dashboard uui-tab[label="Settings"]').click();
        const hide = page.locator('uui-toggle[label="Hide withheld images in pages"]');
        const block = page.locator('uui-toggle[label="Block withheld image files"]');
        await expect(hide).toBeVisible();
        await expect(hide).toHaveJSProperty("checked", original.hideWithheldInPages);
        await expect(block).toHaveJSProperty("checked", original.blockWithheldFiles);

        // Turn both the opposite way and save.
        await hide.click();
        await block.click();
        await page.locator('uui-button[label="Save settings"]').click();
        await expect(page.getByText("Settings saved").first()).toBeVisible();

        const saved = await api<Settings>(page, "GET", "settings");
        expect(saved.hideWithheldInPages).toBe(!original.hideWithheldInPages);
        expect(saved.blockWithheldFiles).toBe(!original.blockWithheldFiles);

        await page.reload();
        await page.locator('umb-media-governance-dashboard uui-tab[label="Settings"]').click();
        await expect(page.locator('uui-toggle[label="Block withheld image files"]')).toHaveJSProperty("checked", !original.blockWithheldFiles);
    } finally {
        await api(page, "PUT", "settings", original);
    }
});
