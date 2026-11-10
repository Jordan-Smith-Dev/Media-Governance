import { expect, test } from "@playwright/test";
import { login, openGovernance } from "./helpers";

// "Campus aerial" is seeded with a consent that has a real PNG on file.
const IMAGE = "Campus aerial";

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("View opens a preview of the evidence image", async ({ page }) => {
    await openGovernance(page, IMAGE);

    await page.locator('uui-button[label="View evidence"]').first().click();

    const dialog = page.locator("dialog.preview");
    await expect(dialog).toBeVisible();
    const img = dialog.locator("img");
    await expect(img).toBeVisible();
    expect(await img.evaluate((i: HTMLImageElement) => i.naturalWidth)).toBeGreaterThan(0);
    await expect(dialog.getByText("recorded in the audit log")).toBeVisible();
    await expect(dialog.locator('uui-button[label="Download"]')).toBeVisible();

    await page.screenshot({ path: "test-results/evidence-preview.png" });

    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
});

test("Download from the preview saves the evidence file", async ({ page }) => {
    await openGovernance(page, IMAGE);
    await page.locator('uui-button[label="View evidence"]').first().click();

    const download = page.waitForEvent("download");
    await page.locator('dialog.preview uui-button[label="Download"]').click();
    expect((await download).suggestedFilename()).toMatch(/^consent-evidence-.+\.(png|pdf|jpg)$/);
});

test("A file that is not really a PDF is refused", async ({ page }) => {
    await openGovernance(page, IMAGE);

    const chooser = page.waitForEvent("filechooser");
    await page.locator('uui-button[label="Replace evidence"]').first().click();
    await (await chooser).setFiles({
        name: "fake.pdf",
        mimeType: "application/pdf",
        buffer: Buffer.from("<html><script>alert(1)</script></html>"),
    });

    await expect(page.getByText(/contents do not match/i).first()).toBeVisible();
});

test("Remove evidence asks first, and cancelling keeps the file", async ({ page }) => {
    await openGovernance(page, IMAGE);

    await page.locator('uui-button[label="Remove evidence"]').first().click();
    await expect(page.getByText(/Remove the evidence for/i).first()).toBeVisible();
    await page.getByRole("button", { name: /cancel/i }).first().click();

    await expect(page.getByText(/Remove the evidence for/i)).toHaveCount(0);
    await expect(page.locator('uui-button[label="View evidence"]').first()).toBeVisible();
});

test("Withdraw asks first, and cancelling keeps the consent", async ({ page }) => {
    await openGovernance(page, IMAGE);

    const withdraw = page.locator('uui-button[label="Withdraw consent"]').first();
    await withdraw.click();
    await expect(page.getByText(/Withdraw consent from/i).first()).toBeVisible();
    await page.getByRole("button", { name: /cancel/i }).first().click();

    await expect(page.getByText(/Withdraw consent from/i)).toHaveCount(0);
    await expect(withdraw).toBeVisible();
});
