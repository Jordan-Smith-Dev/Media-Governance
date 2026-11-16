import { expect, test } from "@playwright/test";
import { login, view } from "./helpers";

const OVERVIEW = view("overview");

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Clicking a column header sorts, and the sort survives a reload", async ({ page }) => {
    await page.goto(OVERVIEW);
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
    await page.goto(`${OVERVIEW}?status=Expired`);

    const statuses = page.locator("uui-table-row .statuses uui-tag").filter({ hasText: /^(Expired|Compliant|No consent|Needs tagging|Expiring soon)$/ });
    await expect(statuses.first()).toBeVisible();
    const texts = await statuses.allInnerTexts();
    expect(texts.length).toBeGreaterThan(0);
    expect(new Set(texts.map((t) => t.trim()))).toEqual(new Set(["Expired"]));
});

test("Typing in the filter is reflected in the address", async ({ page }) => {
    await page.goto(OVERVIEW);
    await page.locator('uui-input[label="Filter images"] input').fill("campus");
    await expect(page).toHaveURL(/q=campus/);
});

test("Status labels explain themselves on hover", async ({ page }) => {
    await page.goto(OVERVIEW);
    const tag = page.locator("uui-table-row .statuses uui-tag").first();
    await expect(tag).toBeVisible();
    expect(((await tag.getAttribute("title")) ?? "").length).toBeGreaterThan(20);
});

test("An empty search explains what to do", async ({ page }) => {
    await page.goto(`${OVERVIEW}?q=zzzz-no-such-image`);
    await expect(page.getByText(/Try a different status or search/i)).toBeVisible();
});

test("Column headings stay in view while the list scrolls", async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 500 });
    await page.goto(OVERVIEW);
    await page.locator("uui-table-row").nth(5).waitFor();
    const heading = page.locator("button.sort", { hasText: "Expiry date" });
    const before = (await heading.boundingBox())!.y;

    await page.locator("uui-table-row").last().scrollIntoViewIfNeeded();
    const after = (await heading.boundingBox())!.y;

    expect(after, "heading scrolled out of view").toBeGreaterThanOrEqual(0);
    expect(after, "heading did not stay put").toBeLessThan(250);
    expect(after).toBeLessThanOrEqual(before);
});

test("The consent and status pills are all one width, so the columns line up", async ({ page }) => {
    await page.goto(OVERVIEW);
    await page.locator("uui-table-row uui-tag.pill").first().waitFor();
    const widths = await page.locator("uui-table-row uui-tag.pill").evaluateAll((tags) => tags.map((t) => Math.round(t.getBoundingClientRect().width)));
    expect(widths.length).toBeGreaterThan(3);
    expect(Math.max(...widths) - Math.min(...widths), `pill widths differ: ${widths}`).toBeLessThanOrEqual(1);
});

test("The page has padding around its content, like other Umbraco views", async ({ page }) => {
    await page.goto(OVERVIEW);
    const padding = await page.locator("umb-media-governance-overview").evaluate((el) => parseFloat(getComputedStyle(el).paddingLeft));
    expect(padding).toBeGreaterThan(8);
});
