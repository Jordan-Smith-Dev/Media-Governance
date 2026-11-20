import { expect, test } from "@playwright/test";
import { api, login, view } from "./helpers";

type Settings = {
    retentionYears: number;
    warningDays: number;
    defaultRetentionYears: number;
    defaultWarningDays: number;
    notificationEmails: string[];
    notificationUserGroupIds: string[];
    blockWithheldFiles: boolean;
    hideWithheldInPages: boolean;
    allowedUserGroups: string[];
};

const restore = (page: import("@playwright/test").Page, s: Settings) =>
    api(page, "PUT", "settings", {
        retentionYears: s.retentionYears,
        warningDays: s.warningDays,
        notificationEmails: s.notificationEmails,
        notificationUserGroupIds: s.notificationUserGroupIds,
    });

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("Only retention, the warning window and recipients can be changed here", async ({ page }) => {
    await page.goto(view("settings"));
    const settings = page.locator("umb-media-governance-settings");
    await expect(settings.locator('uui-input[label="Retention in years"]')).toBeVisible();
    await expect(settings.locator('uui-input[label="Warning window in days"]')).toBeVisible();
    await expect(settings.locator('uui-textarea[label="Notification email addresses"]')).toBeVisible();

    // The editor settings have no switches. (The test site has admin settings on, so an admin also sees their own section: see admin.spec.ts.)
    await expect(settings.locator("uui-box", { hasText: "Retention rules" }).first().locator("uui-toggle")).toHaveCount(0);
    for (const hidden of ["Set in appsettings", "Evidence is kept in"]) await expect(settings.getByText(hidden), hidden).toHaveCount(0);
    await expect(settings.locator("umb-property-layout", { hasText: "Who can use Governance" })).toHaveCount(0);
});

test("Retention shows its appsettings value, warns that it changes existing images, and saves", async ({ page }) => {
    const original = await api<Settings>(page, "GET", "settings");
    try {
        await page.goto(view("settings"));
        const settings = page.locator("umb-media-governance-settings");
        const years = settings.locator('uui-input[label="Retention in years"] input');
        await expect(years).toHaveValue(String(original.retentionYears));
        await expect(settings.getByText(`Starting value from appsettings: ${original.defaultRetentionYears}`)).toBeVisible();

        // No warning until the value changes.
        await expect(settings.locator(".callout")).toHaveCount(0);
        await years.fill(String(original.retentionYears + 1));
        await expect(settings.locator(".callout")).toContainText("affects existing images");

        await page.locator('uui-button[label="Save settings"]').click();
        await expect(page.getByText("Settings saved").first()).toBeVisible();
        expect((await api<Settings>(page, "GET", "settings")).retentionYears).toBe(original.retentionYears + 1);

        await page.reload();
        await expect(page.locator('umb-media-governance-settings uui-input[label="Retention in years"] input')).toHaveValue(String(original.retentionYears + 1));
    } finally {
        await restore(page, original);
    }
});

test("Recipients explain what happens by default, and save", async ({ page }) => {
    const original = await api<Settings>(page, "GET", "settings");
    try {
        await page.goto(view("settings"));
        const settings = page.locator("umb-media-governance-settings");
        await expect(settings.getByText(/Nobody is listed by default, so no email is sent/)).toBeVisible();
        await expect(settings.getByText(/A group is enough on its own/)).toBeVisible();

        const save = page.locator('uui-button[label="Save settings"]');
        await expect(save).toHaveAttribute("disabled", ""); // nothing changed yet

        await settings.locator('uui-textarea[label="Notification email addresses"] textarea').fill("ops@example.com\nOPS@example.com\nqa@example.com");
        await expect(save).not.toHaveAttribute("disabled");
        await save.click();
        await expect(page.getByText("Settings saved").first()).toBeVisible();

        // Duplicates are merged, keeping the first spelling.
        expect((await api<Settings>(page, "GET", "settings")).notificationEmails).toEqual(["ops@example.com", "qa@example.com"]);
    } finally {
        await restore(page, original);
    }
});

test("An invalid retention or email stops the save and says why", async ({ page }) => {
    await page.goto(view("settings"));
    const settings = page.locator("umb-media-governance-settings");
    await settings.locator('uui-input[label="Retention in years"] input').fill("0");
    await expect(settings.getByText("Enter a whole number of years from 1 to 100.")).toBeVisible();
    await expect(page.locator('uui-button[label="Save settings"]')).toHaveAttribute("disabled", "");

    await settings.locator('uui-input[label="Retention in years"] input').fill("3");
    await settings.locator('uui-textarea[label="Notification email addresses"] textarea').fill("not-an-email");
    await expect(settings.getByText(/Not a valid email address/)).toBeVisible();
    await expect(page.locator('uui-button[label="Save settings"]')).toHaveAttribute("disabled", "");
});
