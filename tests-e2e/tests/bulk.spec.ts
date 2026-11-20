import { expect, test } from "@playwright/test";
import { api, login, mediaKeyFor, snapshotImage, view } from "./helpers";

const IMAGE = "Library exterior"; // no people tagged, so both bulk actions apply

type Detail = { customExpiry: string | null; noPeople: boolean };

test.beforeEach(async ({ page }) => {
    await login(page);
});

async function tick(page: import("@playwright/test").Page, name: string) {
    await page.goto(view("overview"));
    await page.locator('uui-input[label="Filter images"] input').fill(name);
    const box = page.locator(`uui-checkbox[aria-label="Select ${name}"]`);
    await expect(box).toBeVisible();
    await box.click();
}

test("Bulk actions only appear once something is ticked", async ({ page }) => {
    await page.goto(view("overview"));
    await page.locator("uui-table-row").first().waitFor();
    await expect(page.getByRole("button", { name: "Set expiry date" })).toHaveCount(0);
    await tick(page, IMAGE);
    await expect(page.getByRole("button", { name: "Set expiry date" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Mark as no people" })).toBeVisible();
});

test("Set expiry opens Umbraco's modal, applies the date, and Remove expiry takes it away", async ({ page }) => {
    test.setTimeout(90_000);
    const key = await mediaKeyFor(page, IMAGE);
    const snap = await snapshotImage(page, key);
    try {
        await tick(page, IMAGE);
        await page.getByRole("button", { name: "Set expiry date" }).click();
        const modal = page.locator("umb-media-governance-expiry-modal");
        await expect(modal).toBeVisible();

        // Asking for a date first: choosing nothing is refused with a message.
        await modal.getByRole("button", { name: "Set expiry", exact: true }).click();
        await expect(modal.getByText("Choose an expiry date.")).toBeVisible();

        await modal.locator("umb-input-date input").fill("2031-05-15");
        await modal.getByRole("button", { name: "Set expiry", exact: true }).click();
        await expect(modal).toBeHidden();

        await expect.poll(async () => (await api<Detail>(page, "GET", `image/${key}`)).customExpiry).toBe("2031-05-15");

        await tick(page, IMAGE);
        await page.getByRole("button", { name: "Set expiry date" }).click();
        await page.locator("umb-media-governance-expiry-modal").getByRole("button", { name: "Remove expiry" }).click();
        await expect.poll(async () => (await api<Detail>(page, "GET", `image/${key}`)).customExpiry).toBeNull();
    } finally {
        await snap.restore();
    }
});

test("Mark as no people asks first, and cancelling changes nothing", async ({ page }) => {
    const key = await mediaKeyFor(page, IMAGE);
    const snap = await snapshotImage(page, key);
    try {
        await tick(page, IMAGE);
        await page.getByRole("button", { name: "Mark as no people" }).click();
        await expect(page.getByText(/as having no people\?/)).toBeVisible();
        await page.getByRole("button", { name: "Cancel" }).click();
        expect((await api<Detail>(page, "GET", `image/${key}`)).noPeople).toBe(snap.original.noPeople);
    } finally {
        await snap.restore();
    }
});

test("Mark as no people clears Needs tagging for the ticked image", async ({ page }) => {
    test.setTimeout(90_000);
    const first = await api<{ items: { mediaKey: string; name: string }[] }>(page, "GET", "overview?status=NeedsTagging&take=1");
    expect(first.items.length, "the seed data should have an image that needs tagging").toBeGreaterThan(0);
    const { mediaKey, name } = first.items[0];
    const snap = await snapshotImage(page, mediaKey);
    try {
        await tick(page, name);
        await page.getByRole("button", { name: "Mark as no people" }).click();
        await page.getByRole("button", { name: "Mark as no people" }).last().click(); // the confirmation's own button
        await expect.poll(async () => (await api<Detail>(page, "GET", `image/${mediaKey}`)).noPeople).toBe(true);
    } finally {
        await snap.restore();
    }
});
