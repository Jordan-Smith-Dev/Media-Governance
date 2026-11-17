import { expect, test, type Page } from "@playwright/test";
import { api, login, mediaKeyFor, openGovernance, personFor, view } from "./helpers";

// The test site switches the optional consent detail on (ConsentDetails, ConsentExpiry, ConsentEvidence and
// PermittedUses Web, Print, Social). With nothing switched on the person modal is just a checkbox: that default is
// covered by the unit tests, which build the settings with every option off.

type PersonFull = {
    id: string;
    name: string;
    consentGiven: boolean;
    consentInForce: boolean;
    consentExpired: boolean;
    consentGivenOn: string | null;
    consentGivenBy: string | null;
    consentExpiresOn: string | null;
    consentPermittedUses: string[] | null;
    hasEvidence: boolean;
    evidenceType: string | null;
};

const modal = (page: Page) => page.locator("umb-media-governance-person-modal");

async function create(page: Page, name: string): Promise<PersonFull> {
    return api<PersonFull>(page, "POST", "people", { name, email: null, phone: null, linkedMemberKey: null, linkedUserKey: null, jobTitle: null });
}

async function person(page: Page, id: string): Promise<PersonFull> {
    const body = await api<{ items: PersonFull[] }>(page, "GET", "people?take=200");
    return body.items.find((p) => p.id === id)!;
}

const PDF = { name: "signed-form.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF") };

test.beforeEach(async ({ page }) => {
    await login(page);
});

test("The consent options show once consent is ticked, and uses are required", async ({ page }) => {
    const p = await create(page, `E2E Blank ${Date.now()}`);
    try {
    await page.goto(view("people"));
    await page.getByRole("button", { name: p.name }).first().click();
    const m = modal(page);
    await expect(m.getByText("Given on")).toHaveCount(0);   // nothing to record until consent is given
    await m.locator("uui-checkbox[label='Consent given']").click();
    await expect(m.getByText("Given on")).toBeVisible();
    await expect(m.getByText("Given by")).toBeVisible();
    await expect(m.getByText("Expires on")).toBeVisible();
    await expect(m.getByText("Permitted uses")).toBeVisible();
    await expect(m.locator("uui-checkbox[label='Print']")).toBeVisible();
    await expect(m.getByText("Evidence", { exact: true })).toBeVisible();

    // Saving without a use is refused and nothing is recorded.
    await m.locator('uui-button[label="Save"]').click();
    await expect(page.getByText("Choose at least one permitted use.").first()).toBeVisible();
    expect((await person(page, p.id)).consentGiven).toBe(false);
    } finally {
        await api(page, "DELETE", `people/${p.id}`);
    }
});

test("Detail is recorded on the person: date given, who gave it, expiry and uses", async ({ page }) => {
    test.setTimeout(90_000);
    const p = await create(page, `E2E Detail ${Date.now()}`);
    try {
        await page.goto(view("people"));
        await page.getByRole("button", { name: p.name }).first().click();
        const m = modal(page);
        await m.locator("uui-checkbox[label='Consent given']").click();
        await m.locator('umb-input-date[label="Given on"] input').fill("2025-03-04");
        await m.locator('uui-select[label="Given by"] select').selectOption("ParentOrGuardian");
        await m.locator('umb-input-date[label="Expires on"] input').fill("2031-03-04");
        await m.locator("uui-checkbox[label='Web']").click();
        await m.locator("uui-checkbox[label='Social']").click();
        await m.locator('uui-button[label="Save"]').click();
        await expect(page.getByText("Person saved").first()).toBeVisible();

        const saved = await person(page, p.id);
        expect(saved.consentGiven).toBe(true);
        expect(saved.consentGivenOn).toBe("2025-03-04");
        expect(saved.consentGivenBy).toBe("ParentOrGuardian");
        expect(saved.consentExpiresOn).toBe("2031-03-04");
        expect(saved.consentPermittedUses?.sort()).toEqual(["Social", "Web"]);
    } finally {
        await api(page, "DELETE", `people/${p.id}`);
    }
});

test("An expired consent shows as expired and makes the images Expired straight away", async ({ page }) => {
    test.setTimeout(120_000);
    const key = await mediaKeyFor(page, "Open day crowd");
    const priya = await personFor(page, "Priya Shah");
    const before = await person(page, priya.id);
    const issues = async () => (await api<{ issues: string[] }>(page, "GET", `image/${key}`)).issues;
    expect(await issues()).not.toContain("Expired");
    try {
        // Given a long time ago and ended a year later: no consent in force today.
        await api(page, "PUT", `people/${priya.id}/consent`, { given: true, givenOn: "2020-01-01", givenBy: "Self", expiresOn: "2021-01-01", permittedUses: ["Web"] });
        const now = await person(page, priya.id);
        expect(now.consentGiven).toBe(true);
        expect(now.consentInForce).toBe(false);
        expect(now.consentExpired).toBe(true);
        expect(await issues()).toContain("Expired");

        await page.goto(view("people"));
        await expect(page.locator("uui-table-row", { hasText: "Priya Shah" }).locator("uui-tag.pill")).toHaveText("Consent expired");

        await openGovernance(page, "Open day crowd");
        const chip = page.locator("umb-media-governance-image-view uui-tag.person", { hasText: "Priya Shah" });
        await expect(chip).toContainText("Consent expired");
        await expect(chip).toHaveAttribute("color", "danger");
    } finally {
        await api(page, "PUT", `people/${priya.id}/consent`, {
            given: before.consentGiven,
            givenOn: before.consentGivenOn,
            givenBy: before.consentGivenBy,
            expiresOn: before.consentExpiresOn,
            permittedUses: before.consentPermittedUses,
        });
    }
    expect(await issues()).not.toContain("Expired");
});

test("An image shows which uses everyone pictured has agreed to", async ({ page }) => {
    await openGovernance(page, "Open day crowd");
    const cleared = page.locator("umb-media-governance-image-view .cleared");
    await expect(cleared).toBeVisible();
    // The seeded people agreed to every listed use, so each is cleared.
    for (const use of ["Web", "Print", "Social"]) await expect(cleared.locator("uui-tag", { hasText: use })).toHaveAttribute("color", "positive");
});

test("A use someone has not agreed to is not cleared for the image", async ({ page }) => {
    test.setTimeout(90_000);
    const priya = await personFor(page, "Priya Shah");
    const before = await person(page, priya.id);
    try {
        await api(page, "PUT", `people/${priya.id}/consent`, { given: true, givenOn: before.consentGivenOn, givenBy: before.consentGivenBy, expiresOn: before.consentExpiresOn, permittedUses: ["Web"] });
        await openGovernance(page, "Open day crowd");
        const cleared = page.locator("umb-media-governance-image-view .cleared");
        await expect(cleared.locator("uui-tag", { hasText: "Web" })).toHaveAttribute("color", "positive");
        await expect(cleared.locator("uui-tag", { hasText: "Print" })).toHaveAttribute("color", "danger");
    } finally {
        await api(page, "PUT", `people/${priya.id}/consent`, { given: before.consentGiven, givenOn: before.consentGivenOn, givenBy: before.consentGivenBy, expiresOn: before.consentExpiresOn, permittedUses: before.consentPermittedUses });
    }
});

test("Evidence can be uploaded, viewed, listed in its history and removed, and it is never public", async ({ page }) => {
    test.setTimeout(120_000);
    const p = await create(page, `E2E Evidence ${Date.now()}`);
    try {
        await page.goto(view("people"));
        await page.getByRole("button", { name: p.name }).first().click();
        const m = modal(page);
        await expect(m.locator("uui-file-dropzone")).toBeVisible();      // the dropzone is the empty state
        await expect(m.locator("uui-ref-node.evidence-line")).toHaveCount(0);
        await expect(m.getByText(/Kept in: Media folder "governance-evidence" \(never served to the public\)/)).toBeVisible();

        await m.locator('input[type="file"]').setInputFiles(PDF);
        await expect(page.getByText("Evidence uploaded").first()).toBeVisible();
        await expect(m.locator('uui-ref-node[name^="PDF"]')).toBeVisible();
        expect((await person(page, p.id)).hasEvidence).toBe(true);

        // Opening it goes through the authenticated API and is recorded.
        const [viewer] = await Promise.all([page.context().waitForEvent("page"), m.locator('uui-button[label="View evidence"]').click()]);
        await viewer.close();
        await m.locator('uui-button[label="History"]').click();
        await expect(m.locator("uui-table.history uui-table-row", { hasText: "Uploaded" })).toBeVisible();
        await expect(m.locator("uui-table.history uui-table-row", { hasText: "Viewed" })).toBeVisible();

        // Replace with a PNG, then remove (asks first).
        await m.locator('input[type="file"]').setInputFiles({ name: "scan.png", mimeType: "image/png", buffer: Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3]) });
        await expect(m.locator('uui-ref-node[name^="PNG"]')).toBeVisible();
        await m.locator('uui-button[label="Remove evidence"]').click();
        await expect(page.getByText("Remove the evidence?")).toBeVisible();
        await page.getByRole("button", { name: "Remove evidence" }).last().click();
        await expect(m.locator("uui-ref-node.evidence-line")).toHaveCount(0);
        expect((await person(page, p.id)).hasEvidence).toBe(false);
    } finally {
        await api(page, "DELETE", `people/${p.id}`);
    }
});

test("A file that is not a real PDF, PNG or JPG is refused", async ({ page }) => {
    const p = await create(page, `E2E Bad file ${Date.now()}`);
    try {
        await page.goto(view("people"));
        await page.getByRole("button", { name: p.name }).first().click();
        await modal(page).locator('input[type="file"]').setInputFiles({ name: "form.pdf", mimeType: "application/pdf", buffer: Buffer.from("this is not a pdf") });
        await expect(page.getByText(/do not match its type/).first()).toBeVisible();
        expect((await person(page, p.id)).hasEvidence).toBe(false);
    } finally {
        await api(page, "DELETE", `people/${p.id}`);
    }
});

test("The evidence folder in the media area is never served, even to a signed-in user", async ({ page }) => {
    for (const path of ["/media/governance-evidence/", "/media/governance-evidence/anything/at-all.pdf", "/MEDIA/Governance-Evidence/x.png"]) {
        const res = await page.request.get(path);
        expect(res.status(), path).toBe(404);
    }
});

test("Deleting a person also deletes their evidence", async ({ page }) => {
    test.setTimeout(90_000);
    const p = await create(page, `E2E Delete ${Date.now()}`);
    await page.goto(view("people"));
    await page.getByRole("button", { name: p.name }).first().click();
    await modal(page).locator('input[type="file"]').setInputFiles(PDF);
    await expect(page.getByText("Evidence uploaded").first()).toBeVisible();
    await modal(page).locator('uui-button[label="Delete"]').click();
    await page.getByRole("button", { name: "Delete" }).last().click();
    await expect(page.getByText("Person deleted").first()).toBeVisible();
    const body = await api<{ items: PersonFull[] }>(page, "GET", "people?take=200");
    expect(body.items.find((x) => x.id === p.id)).toBeUndefined();
    expect((await page.request.fetch(`/umbraco/umbracocommunitymediagovernance/api/v1/people/${p.id}/evidence`)).status()).not.toBe(200);
});

test("Download their data gives a zip with everything held about the person", async ({ page }) => {
    test.setTimeout(90_000);
    const p = await create(page, `E2E Export ${Date.now()}`);
    try {
        await page.goto(view("people"));
        await page.getByRole("button", { name: p.name }).first().click();
        await modal(page).locator('input[type="file"]').setInputFiles(PDF);
        await expect(page.getByText("Evidence uploaded").first()).toBeVisible();

        const [download] = await Promise.all([page.waitForEvent("download"), modal(page).locator('uui-button[label="Download their data"]').click()]);
        expect(download.suggestedFilename()).toMatch(/^person-data-e2e-export-\d+\.zip$/);
        const path = await download.path();
        const fs = await import("node:fs");
        const bytes = fs.readFileSync(path!);
        expect(bytes.subarray(0, 2).toString()).toBe("PK");          // a zip
        expect(bytes.toString("latin1")).toContain("person-data.json");
        expect(bytes.toString("latin1")).toContain("evidence.pdf");   // the evidence file is in it
    } finally {
        await api(page, "DELETE", `people/${p.id}`);
    }
});

test("Settings lists the consent options that are switched on, read-only", async ({ page }) => {
    await page.goto(view("settings"));
    const s = page.locator("umb-media-governance-settings");
    await expect(s.getByText("Consent detail")).toBeVisible();
    for (const t of ["Date given and who gave it", "Expiry", "Evidence upload", "Permitted uses (Web, Print, Social)"]) await expect(s.getByText(t)).toBeVisible();
    await expect(s.getByText("Evidence is kept in")).toBeVisible();
    await expect(s.locator("uui-toggle")).toHaveCount(0);   // nothing to switch here: it all comes from appsettings
});
