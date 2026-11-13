import { expect, test } from "@playwright/test";
import { login, openGovernance } from "./helpers";

const DASHBOARD = "/umbraco/section/media/dashboard/media-governance";

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Tagged people with no consent are called out, and the button starts their consent", async ({ page }) => {
    await openGovernance(page, "Campus aerial");
    const notice = page.locator("umb-media-governance-image-view .notice");
    await expect(notice).toBeVisible();
    await expect(notice).toContainText("no current consent");

    await notice.getByRole("button", { name: /Add consent for/ }).click();
    await expect(page.locator("umb-media-governance-image-view .consent-form")).toBeVisible();
    await expect(notice).toBeHidden(); // the form replaces the prompt while it is open
});

test("The +N on a status explains the other statuses", async ({ page }) => {
    await page.goto(DASHBOARD);
    const more = page.locator("uui-table-row .statuses uui-tag[look=secondary]").first();
    await expect(more).toBeVisible();
    const tip = (await more.getAttribute("title")) ?? "";
    expect(tip).toMatch(/: .{20,}/); // "Status: explanation"
});

test("The +N on people lists the names that are hidden", async ({ page }) => {
    await page.goto(DASHBOARD);
    const more = page.locator("uui-table-row .chips uui-tag[look=placeholder]").first();
    await expect(more).toBeVisible();
    expect(((await more.getAttribute("title")) ?? "").length).toBeGreaterThan(3);
});

test("The history table header is solid, so rows do not show through it", async ({ page }) => {
    await openGovernance(page, "Graduation 2023");
    await page.locator('uui-button[label="View evidence"]').first().click();
    await page.locator("dialog.preview[open]").waitFor();
    await page.locator("dialog.preview[open] .preview-foot uui-button[label=Close]").click();
    await page.locator("button.link", { hasText: "History" }).first().click();
    const bg = await page.locator("dialog.history uui-table-head-cell").first().evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg).not.toBe("rgba(0, 0, 0, 0)");
    expect(bg).not.toBe("transparent");
});
