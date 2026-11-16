import { expect, test } from "@playwright/test";
import { login, openGovernance, view } from "./helpers";

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("A tagged person without consent shows red, and one with consent shows green", async ({ page }) => {
    await openGovernance(page, "Lecture theatre");
    const tom = page.locator("umb-media-governance-image-view uui-tag.person").filter({ hasText: "Tom Reyes" });
    await expect(tom).toHaveAttribute("color", "danger");
    await expect(tom).toContainText("No consent");

    await openGovernance(page, "Headshot - Verna Burke");
    const verna = page.locator("umb-media-governance-image-view uui-tag.person").filter({ hasText: "Verna Burke" });
    await expect(verna).toHaveAttribute("color", "positive");
    await expect(verna).toContainText("Consent given");
});

test("The consent tag text is centred and its remove icon is light on the coloured tag", async ({ page }) => {
    await openGovernance(page, "Lecture theatre");
    const tag = page.locator("umb-media-governance-image-view uui-tag.person").first();
    await expect(tag).toBeVisible();
    const row = tag.locator(".person-row");
    const tagBox = (await tag.boundingBox())!;
    const rowBox = (await row.boundingBox())!;
    // The content sits in the vertical middle of the tag.
    expect(Math.abs(tagBox.y + tagBox.height / 2 - (rowBox.y + rowBox.height / 2))).toBeLessThanOrEqual(2);
    // The remove icon takes the tag's contrast colour (white), not the default dark colour.
    const icon = await tag.locator("uui-icon").evaluate((el) => getComputedStyle(el).color);
    expect(icon).toMatch(/^rgb\(2[0-5]\d, 2[0-5]\d, 2[0-5]\d\)$/);
});

test("The +N on a status explains the other statuses", async ({ page }) => {
    await page.goto(view("overview"));
    const more = page.locator("uui-table-row .statuses uui-tag[look=secondary]").first();
    await expect(more).toBeVisible();
    const tip = (await more.getAttribute("title")) ?? "";
    expect(tip).toMatch(/: .{20,}/); // "Status: explanation"
});

test("The +N on people lists the names that are hidden", async ({ page }) => {
    await page.goto(view("overview"));
    const more = page.locator("uui-table-row .chips uui-tag[look=placeholder]").first();
    await expect(more).toBeVisible();
    expect(((await more.getAttribute("title")) ?? "").length).toBeGreaterThan(3);
});

test("States with no value are tags, not loose text", async ({ page }) => {
    await page.goto(view("overview"));
    await page.locator("uui-table-row").first().waitFor();
    await expect(page.locator("uui-table-row uui-tag.pill", { hasText: "Not required" }).first()).toBeVisible();
    await expect(page.locator("uui-table-row uui-tag", { hasText: "No people" }).first()).toBeVisible();
    await expect(page.locator("uui-table-row uui-tag", { hasText: "Not tagged yet" }).first()).toBeVisible();
    await expect(page.locator("uui-table-row uui-tag.pill", { hasText: "Unknown" }).first()).toBeVisible();
});

test("Pages affected lists the pages using withheld images", async ({ page }) => {
    await page.goto(view("pages"));
    await expect(page.locator("umb-media-governance-pages uui-table-row").first()).toBeVisible();
    await expect(page.getByText(/pages? affected/)).toBeVisible();
});
