import { expect, test, type Page } from "@playwright/test";
import { api, apiStatus, login, view } from "./helpers";

// The test site switches admin settings on (AllowAdminSettings) and the test user is in the admin group.
// With the switch off (the default) the box does not exist: that is covered by the unit tests.

type AdminValues = {
    blockWithheldFiles: boolean;
    hideWithheldInPages: boolean;
    showStatusSigns: boolean;
    colourTreeIcons: boolean;
    showFolderMarkers: boolean;
    consentDetails: boolean;
    consentExpiry: boolean;
    consentEvidence: boolean;
    permittedUses: string[];
};
type SettingsDto = { adminSettings: { current: AdminValues; defaults: AdminValues } | null };

const admin = (page: Page) => page.locator("umb-media-governance-settings .admin");
const save = (page: Page) => admin(page).locator('uui-button[label="Save admin settings"]');
const toggle = (page: Page, label: string) => admin(page).locator(`uui-toggle[label="${label}"]`);

/** Puts the admin settings back to what they were, whatever a test did. */
const restore = (page: Page, original: AdminValues) => api(page, "PUT", "settings/admin", original);

async function current(page: Page): Promise<AdminValues> {
    return (await api<SettingsDto>(page, "GET", "settings")).adminSettings!.current;
}

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Admins see an Admin settings section, with the protected settings left out", async ({ page }) => {
    await page.goto(view("settings"));
    await expect(admin(page)).toBeVisible();
    await expect(admin(page).getByText("Admin settings.", { exact: false })).toBeVisible();
    for (const box of ["Consent detail", "Your website", "Media tree"]) await expect(admin(page).locator("uui-box", { hasText: box }).first()).toBeVisible();

    // Who can use Governance and where evidence is kept are appsettings only, and say so.
    await expect(admin(page).getByText("can only be changed in appsettings")).toBeVisible();
    // ...and there is no setting row for them.
    for (const hidden of ["Who can use Governance", "Evidence is kept in", "AllowedUserGroups", "EvidenceStoragePath"])
        await expect(admin(page).locator("umb-property-layout", { hasText: hidden }), hidden).toHaveCount(0);
    await expect(save(page)).toHaveAttribute("disabled", "");   // nothing changed yet
});

test("The settings an editor can always change are unchanged by the admin section", async ({ page }) => {
    await page.goto(view("settings"));
    const s = page.locator("umb-media-governance-settings");
    await expect(s.locator('uui-input[label="Retention in years"]')).toBeVisible();
    await expect(s.locator("uui-box", { hasText: "Retention rules" }).first().locator("uui-toggle")).toHaveCount(0);
    await expect(s.locator("uui-box", { hasText: "Notifications" }).first().locator("uui-toggle")).toHaveCount(0);
});

test("A change is saved, marked as changed here with what appsettings says, and survives a reload; Reset puts it back", async ({ page }) => {
    test.setTimeout(120_000);
    const original = await current(page);
    try {
        await page.goto(view("settings"));
        await expect(admin(page).getByText("Changed here")).toHaveCount(0);   // everything follows appsettings to begin with

        await toggle(page, "Coloured image icons").click();
        await expect(save(page)).not.toHaveAttribute("disabled");
        await save(page).click();
        await expect(page.getByText("Admin settings saved").first()).toBeVisible();

        expect((await current(page)).colourTreeIcons).toBe(!original.colourTreeIcons);
        await page.reload();
        const row = admin(page).locator("umb-property-layout", { hasText: "Coloured image icons" });
        await expect(row.getByText("Changed here")).toBeVisible();
        await expect(row.getByText(/appsettings says: (Coloured|Plain)/)).toBeVisible();

        await row.locator('uui-button[label="Reset to appsettings"]').click();
        await expect(row.getByText("Changed here")).toHaveCount(0);
        await save(page).click();
        await expect(page.getByText("Admin settings saved").first()).toBeVisible();
        expect((await current(page)).colourTreeIcons).toBe(original.colourTreeIcons);
    } finally {
        await restore(page, original);
    }
});

test("Switching off a protection asks first, and cancelling changes nothing", async ({ page }) => {
    test.setTimeout(120_000);
    const original = await current(page);
    try {
        await page.goto(view("settings"));
        await toggle(page, "Block withheld image files").click();
        await expect(admin(page).getByText("Withheld images can still be reached by their address")).toBeVisible();
        await save(page).click();
        await expect(page.getByText("Switch off a protection?")).toBeVisible();
        await expect(page.getByText(/withheld image files will be served to the public again/)).toBeVisible();
        await page.getByRole("button", { name: "Cancel" }).last().click();
        expect((await current(page)).blockWithheldFiles).toBe(original.blockWithheldFiles);

        await save(page).click();
        await page.getByRole("button", { name: "Switch off and save" }).click();
        await expect(page.getByText("Admin settings saved").first()).toBeVisible();
        expect((await current(page)).blockWithheldFiles).toBe(false);
    } finally {
        await restore(page, original);
    }
});

test("Switching a protection on again does not ask", async ({ page }) => {
    test.setTimeout(120_000);
    const original = await current(page);
    try {
        await api(page, "PUT", "settings/admin", { ...original, hideWithheldInPages: false });
        await page.goto(view("settings"));
        await toggle(page, "Hide withheld images in pages").click();   // back on
        await save(page).click();
        await expect(page.getByText("Admin settings saved").first()).toBeVisible();
        await expect(page.getByText("Switch off a protection?")).toHaveCount(0);
    } finally {
        await restore(page, original);
    }
});

test("A change takes effect straight away, with no deployment: switching evidence off hides it on a person", async ({ page }) => {
    test.setTimeout(150_000);
    const original = await current(page);
    expect(original.consentEvidence, "the test site switches evidence on").toBe(true);
    try {
        await page.goto(view("settings"));
        await toggle(page, "Evidence upload").click();
        await save(page).click();
        await expect(page.getByText("Admin settings saved").first()).toBeVisible();

        await page.goto(view("people"));
        await page.getByRole("button", { name: "Tom Reyes" }).first().click();
        const modal = page.locator("umb-media-governance-person-modal");
        await modal.locator("uui-checkbox[label='Consent given']").waitFor();
        await expect(modal.locator("uui-box", { hasText: "Evidence" })).toHaveCount(0);
    } finally {
        await restore(page, original);
    }
});

test("Permitted uses an admin adds appear on a person, and too many characters are refused", async ({ page }) => {
    test.setTimeout(150_000);
    const original = await current(page);
    try {
        await page.goto(view("settings"));
        const input = admin(page).locator('uui-input[label="Permitted uses"] input');
        await input.fill(`${original.permittedUses.join(", ")}, Radio`);
        await expect(admin(page).locator(".chips uui-tag", { hasText: "Radio" })).toBeVisible();
        await save(page).click();
        await expect(page.getByText("Admin settings saved").first()).toBeVisible();

        await page.goto(view("people"));
        await page.getByRole("button", { name: "Tom Reyes" }).first().click();
        const modal = page.locator("umb-media-governance-person-modal");
        await modal.locator("uui-checkbox[label='Consent given']").waitFor();
        // consent has to be given to see its uses: tick it (this is not saved, the modal is cancelled)
        const given = modal.locator("uui-checkbox[label='Consent given']");
        if (!(await given.evaluate((e) => (e as unknown as { checked: boolean }).checked))) await given.click();
        await expect(modal.locator("uui-checkbox[label='Radio']")).toBeVisible();
        await modal.locator('uui-button[label="Cancel"]').click();

        await page.goto(view("settings"));
        await admin(page).locator('uui-input[label="Permitted uses"] input').fill("x".repeat(31));
        await expect(admin(page).getByText(/is too long/)).toBeVisible();
        await expect(save(page)).toHaveAttribute("disabled", "");
    } finally {
        await restore(page, original);
    }
});

test("The server refuses admin changes that are not valid", async ({ page }) => {
    const original = await current(page);
    expect(await apiStatus(page, "PUT", "settings/admin", { ...original, permittedUses: ["bad,use"] })).toBe(400);
    expect((await current(page)).permittedUses).toEqual(original.permittedUses);
});
