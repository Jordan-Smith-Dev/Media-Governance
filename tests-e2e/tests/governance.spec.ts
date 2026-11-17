import { expect, test } from "@playwright/test";
import { api, login, mediaKeyFor, openGovernance, snapshotImage, GOVERNANCE } from "./helpers";

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Governance in the Media sidebar opens the page, with a Log Viewer style header", async ({ page }) => {
    await page.goto("/umbraco/section/media");
    // Wait for the sidebar to finish loading (Umbraco redirects the section once it has), then open Governance.
    await expect(page.getByText("Recycle Bin")).toBeVisible();
    await page.waitForURL(/\/umbraco\/section\/media/);
    await page.getByText("Governance", { exact: true }).first().click();
    await expect(page).toHaveURL(new RegExp(`${GOVERNANCE}`));

    const header = page.locator("umb-workspace-editor");
    await expect(header.getByText("Governance", { exact: true }).first()).toBeVisible();
    for (const tab of ["Overview", "People", "Pages affected", "Settings"]) await expect(header.locator("uui-tab", { hasText: tab })).toBeVisible();
    // Not a second tab beside Media's own dashboard any more.
    await expect(page.getByText("Media governance")).toHaveCount(0);
    await expect(header.getByRole("tab", { name: "Media", exact: true })).toHaveCount(0);
});

test("The tabs switch the view", async ({ page }) => {
    await page.goto(`${GOVERNANCE}/view/overview`);
    await page.locator("umb-workspace-editor uui-tab", { hasText: "People" }).click();
    await expect(page).toHaveURL(/view\/people/);
    await expect(page.locator("umb-media-governance-people")).toBeVisible();
    await page.locator("umb-workspace-editor uui-tab", { hasText: "Settings" }).click();
    await expect(page).toHaveURL(/view\/settings/);
    await expect(page.locator("umb-media-governance-settings")).toBeVisible();
});

test("The image has no Compliance section, and Expiry starts at the calculated date", async ({ page }) => {
    await openGovernance(page, "Library exterior");
    const view = page.locator("umb-media-governance-image-view");
    await expect(view.getByText("Compliance", { exact: true })).toHaveCount(0);
    await expect(view.getByText("Custom expiry")).toHaveCount(0);
    await expect(view.getByText("Expiry", { exact: true })).toBeVisible();
    await expect(view.locator('umb-input-date[label="Expiry"] input')).not.toHaveValue("");
});

test("An expired image shows an Expired tag beside its Expiry", async ({ page }) => {
    await openGovernance(page, "Campus aerial");
    await expect(page.locator("umb-media-governance-image-view .expiry uui-tag")).toContainText("Expired");
});

test("An image still in date shows how long is left in a green pill, like Expired is red", async ({ page }) => {
    await openGovernance(page, "Library exterior");
    const tag = page.locator("umb-media-governance-image-view .expiry uui-tag");
    await expect(tag).toHaveText(/^in \d+ /);
    await expect(tag).toHaveAttribute("color", "positive");
    await expect(tag).toHaveAttribute("look", "primary");
});

test("Choosing a different Expiry stores it, and choosing the calculated date goes back to the retention period", async ({ page }) => {
    test.setTimeout(90_000);
    const key = await mediaKeyFor(page, "Library exterior");
    const snap = await snapshotImage(page, key);
    type Detail = { customExpiry: string | null; defaultExpiry: string };
    try {
        await openGovernance(page, "Library exterior");
        const input = page.locator('umb-media-governance-image-view umb-input-date[label="Expiry"] input');
        const calculated = (await api<Detail>(page, "GET", `image/${key}`)).defaultExpiry;
        expect(calculated).toBeTruthy();

        await input.fill("2031-01-01");
        await expect.poll(async () => (await api<Detail>(page, "GET", `image/${key}`)).customExpiry).toBe("2031-01-01");

        await input.fill(calculated);
        await expect.poll(async () => (await api<Detail>(page, "GET", `image/${key}`)).customExpiry).toBeNull();
    } finally {
        await snap.restore();
    }
});

test("Tagging searches people as you type instead of loading everyone", async ({ page }) => {
    await openGovernance(page, "Library exterior");
    const view = page.locator("umb-media-governance-image-view");
    await view.locator('uui-input[label="Search people to tag"] input').fill("priya");
    await expect(view.getByRole("button", { name: "Tag Priya Shah" })).toBeVisible();
    await expect(view.getByRole("button", { name: "Tag Tom Reyes" })).toHaveCount(0);
});

test("Pages affected shows only a few images per page, with the rest behind a +N", async ({ page }) => {
    await page.goto(`${GOVERNANCE}/view/pages`);
    const rows = page.locator("umb-media-governance-pages uui-table-row");
    await expect(rows.first()).toBeVisible();
    // Never more than three named images in a row; any beyond that are a "+N" tag with a tooltip.
    const counts = await page.locator("umb-media-governance-pages .images").evaluateAll((els) => els.map((e) => e.querySelectorAll("a.image").length));
    expect(Math.max(...counts)).toBeLessThanOrEqual(3);
});
