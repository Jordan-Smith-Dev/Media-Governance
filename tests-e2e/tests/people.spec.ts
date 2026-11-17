import { expect, test } from "@playwright/test";
import { api, login, mediaKeyFor, openGovernance, personFor, tickUse, view } from "./helpers";

const modal = (page: import("@playwright/test").Page) => page.locator("umb-media-governance-person-modal");

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("The list shows who has given consent", async ({ page }) => {
    await page.goto(view("people"));
    const tom = page.locator("uui-table-row", { hasText: "Tom Reyes" });
    const amara = page.locator("uui-table-row", { hasText: "Amara Okafor" });
    await expect(tom.locator("uui-tag.pill")).toHaveText("No consent");
    await expect(amara.locator("uui-tag.pill")).toHaveText("Consent given");
    await expect(page.getByText(/Showing \d+ of \d+ people/)).toBeVisible();
});

test("Searching narrows the list", async ({ page }) => {
    await page.goto(view("people"));
    await page.locator('uui-input[label="Search people"] input').fill("priya");
    await expect(page.locator("umb-media-governance-people uui-table-row")).toHaveCount(1); // only Priya
    await expect(page.getByRole("button", { name: "Priya Shah" }).first()).toBeVisible();
});

test("Opening a person slides out Umbraco's modal with their details, consent and tagged images", async ({ page }) => {
    await page.goto(view("people"));
    await page.getByRole("button", { name: "Tom Reyes" }).first().click();
    await expect(modal(page)).toBeVisible();
    await expect(modal(page).getByText("Details", { exact: true })).toBeVisible();
    await expect(modal(page).getByText("Consent", { exact: true })).toBeVisible();
    await expect(modal(page).getByText(/Tagged in \d+ images?/)).toBeVisible();
    await expect(modal(page).getByRole("link", { name: "Lecture theatre" })).toBeVisible();
    await expect(modal(page).locator("uui-checkbox[label='Consent given']")).not.toHaveJSProperty("checked", true);
});

test("Giving consent is saved on the person and changes every image they are in", async ({ page }) => {
    test.setTimeout(90_000);
    const tom = await personFor(page, "Tom Reyes");
    const key = await mediaKeyFor(page, "Lecture theatre");
    const issues = async () => (await api<{ issues: string[] }>(page, "GET", `image/${key}`)).issues;
    expect(await issues()).toContain("NoConsent");
    try {
        await page.goto(view("people"));
        await page.getByRole("button", { name: "Tom Reyes" }).first().click();
        await modal(page).locator("uui-checkbox[label='Consent given']").click();
        await tickUse(modal(page), "Web");   // the test site lists permitted uses, so one is needed
        await modal(page).locator('uui-button[label="Save"]').click();
        await expect(page.getByText("Person saved").first()).toBeVisible();

        expect((await personFor(page, "Tom Reyes")).consentGiven).toBe(true);
        expect(await issues()).not.toContain("NoConsent");
        await expect(page.locator("uui-table-row", { hasText: "Tom Reyes" }).locator("uui-tag.pill")).toHaveText("Consent given");
    } finally {
        await api(page, "PUT", `people/${tom.id}/consent`, { given: false });
    }
});

test("Cancelling the modal changes nothing", async ({ page }) => {
    await page.goto(view("people"));
    await page.getByRole("button", { name: "Tom Reyes" }).first().click();
    await modal(page).locator("uui-checkbox[label='Consent given']").click();
    await modal(page).locator('uui-button[label="Cancel"]').click();
    await expect(modal(page)).toBeHidden();
    expect((await personFor(page, "Tom Reyes")).consentGiven).toBe(false);
});

test("A tagged person on an image links to their record, which opens", async ({ page }) => {
    await openGovernance(page, "Lecture theatre");
    await page.locator("umb-media-governance-image-view uui-tag.person").getByRole("link", { name: "Tom Reyes" }).click();
    await expect(page).toHaveURL(/view\/people\?person=/);
    await expect(modal(page)).toBeVisible();
    await expect(modal(page).getByRole("textbox", { name: "Name" })).toHaveValue("Tom Reyes");
});

test("Delete asks first, and cancelling keeps the person", async ({ page }) => {
    await page.goto(view("people"));
    await page.getByRole("button", { name: "Tom Reyes" }).first().click();
    await modal(page).locator('uui-button[label="Delete"]').click();
    await expect(page.getByText("Delete Tom Reyes?")).toBeVisible();
    await page.getByRole("button", { name: "Cancel" }).last().click();
    await personFor(page, "Tom Reyes"); // still there
});

test("A person can be added with consent, and shows in the list", async ({ page }) => {
    test.setTimeout(90_000);
    const name = `E2E Person ${Date.now()}`;
    let id: string | undefined;
    try {
        await page.goto(view("people"));
        await page.getByRole("button", { name: "Add person" }).click();
        await modal(page).getByRole("textbox", { name: "Name" }).fill(name);
        await modal(page).locator("uui-checkbox[label='Consent given']").click();
        await tickUse(modal(page), "Web");
        await modal(page).locator('uui-button[label="Save"]').click();
        await expect(page.getByText("Person saved").first()).toBeVisible();

        const row = page.locator("uui-table-row", { hasText: name });
        await expect(row.locator("uui-tag.pill")).toHaveText("Consent given");
        id = (await personFor(page, name)).id;
    } finally {
        if (id) await api(page, "DELETE", `people/${id}`);
    }
});
