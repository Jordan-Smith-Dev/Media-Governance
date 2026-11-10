import { expect, test } from "@playwright/test";
import { login } from "./helpers";

const DASHBOARD = "/umbraco/section/media/dashboard/media-governance";

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Clicking a column header sorts, and the sort survives a reload", async ({ page }) => {
    await page.goto(DASHBOARD);
    const expiry = page.locator("button.sort", { hasText: "Expiry date" });
    await expiry.click();

    await expect(page).toHaveURL(/sort=Expiry/);
    await expect(page.locator('uui-table-head-cell[aria-sort="ascending"]')).toHaveCount(1);

    await expiry.click();
    await expect(page).toHaveURL(/desc=true/);
    await expect(page.locator('uui-table-head-cell[aria-sort="descending"]')).toHaveCount(1);

    await page.reload();
    await expect(page.locator('uui-table-head-cell[aria-sort="descending"]')).toHaveCount(1);
});

test("A status in the address shows only that status", async ({ page }) => {
    await page.goto(`${DASHBOARD}?status=Expired`);

    const statuses = page.locator("uui-table-row .statuses uui-tag").filter({ hasText: /^(Expired|Compliant|No consent|Needs tagging|Expiring soon)$/ });
    await expect(statuses.first()).toBeVisible();
    const texts = await statuses.allInnerTexts();
    expect(texts.length).toBeGreaterThan(0);
    expect(new Set(texts.map((t) => t.trim()))).toEqual(new Set(["Expired"]));
});

test("Typing in the filter is reflected in the address", async ({ page }) => {
    await page.goto(DASHBOARD);
    await page.locator("uui-input[label=\"Filter images\"] input").fill("campus");
    await expect(page).toHaveURL(/q=campus/);
});

test("Status labels explain themselves on hover", async ({ page }) => {
    await page.goto(DASHBOARD);
    const tag = page.locator("uui-table-row .statuses uui-tag").first();
    await expect(tag).toBeVisible();
    expect(((await tag.getAttribute("title")) ?? "").length).toBeGreaterThan(20);
});

test("An empty search explains what to do", async ({ page }) => {
    await page.goto(`${DASHBOARD}?q=zzzz-no-such-image`);
    await expect(page.getByText(/Try a different status or search/i)).toBeVisible();
});
