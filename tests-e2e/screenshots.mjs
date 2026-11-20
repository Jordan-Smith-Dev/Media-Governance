// Takes the screenshots used in the README, for both modes of the package:
//
//   default   consent is a yes or no on the person (every option off)      -> SIMPLE_URL
//   detailed  the optional consent detail switched on                      -> DETAILED_URL
//
// Start two copies of the test site first (see the README, "Screenshots"), each on its own database:
//
//   detailed (options on, the test site's own settings):
//     ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__umbracoDbDSN="Data Source=|DataDirectory|/Umbraco.shots-detailed.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True" \
//       dotnet run --no-build --project ../src/Umbraco.Community.MediaGovernance.TestSite --urls "https://localhost:44400;http://localhost:5200"
//   default (the options switched off again with environment variables):
//     (same, with)  Umbraco__Community__MediaGovernance__ConsentDetails=false  ...__ConsentExpiry=false  ...__ConsentEvidence=false
//                   ...__PermittedUses__0=" "  ...__PermittedUses__1=" "  ...__PermittedUses__2=" "
//       --urls "https://localhost:44401;http://localhost:5201"
//
// Then: npm run screenshots   (images go to ../docs/images)
import { chromium } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const OUT = process.env.OUT ?? path.join(here, "..", "docs", "images");
const DETAILED = process.env.DETAILED_URL ?? "https://localhost:44400";
const SIMPLE = process.env.SIMPLE_URL ?? "https://localhost:44401";
const EMAIL = process.env.MG_EMAIL ?? "admin@example.com";
const PASSWORD = process.env.MG_PASSWORD ?? "1234567890";
const API = "/umbraco/umbracocommunitymediagovernance/api/v1";
const GOVERNANCE = "/umbraco/section/media/workspace/media-governance";

fs.mkdirSync(OUT, { recursive: true });
const browser = await chromium.launch({ channel: "chrome" });

// A small but valid PDF, so the evidence box shows a real file.
const PDF = Buffer.from("%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

async function open(base, width, height) {
    const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width, height } });
    const page = await context.newPage();
    const holder = {};
    page.on("request", (r) => { const a = r.headers()["authorization"]; if (a?.startsWith("Bearer ")) holder.token = a; });
    await page.goto(`${base}/umbraco`);
    await page.getByLabel(/e-?mail/i).first().fill(EMAIL);
    await page.locator("#password-input").fill(PASSWORD);
    await page.keyboard.press("Enter");
    await page.waitForURL(/\/umbraco\/section\//, { timeout: 60000 });
    await page.waitForTimeout(2500);
    const call = async (method, p, data) => {
        const res = await page.request.fetch(`${base}${API}/${p}`, { method, headers: { Authorization: holder.token ?? "" }, data });
        if (!res.ok()) throw new Error(`${method} ${p} -> ${res.status()}`);
        const text = await res.text();
        return text ? JSON.parse(text) : undefined;
    };
    return { page, call, base };
}

const save = (name) => path.join(OUT, name);

/** The right-hand sidebar modal, cropped to its content. */
async function shootModal(page, name) {
    const box = await page.locator("umb-media-governance-person-modal uui-box").last().boundingBox();
    const height = Math.min(Math.ceil(box.y + box.height + 28), page.viewportSize().height);
    await page.screenshot({ path: save(name), clip: { x: 600, y: 0, width: page.viewportSize().width - 600, height } });
}

async function openPerson(page, base, name) {
    await page.goto(`${base}${GOVERNANCE}/view/people`);
    await page.getByRole("button", { name }).first().click();
    await page.locator("umb-media-governance-person-modal").waitFor();
    await page.waitForTimeout(2000);
}

async function imageUrl(call, base, imageName) {
    const items = (await call("GET", "overview?take=500")).items;
    return `${base}/umbraco/section/media/workspace/media/edit/${items.find((i) => i.name === imageName).mediaKey}/invariant/view/governance`;
}

// ------------------------------------------------------------------ default: consent is a yes or no
{
    const { page, call, base } = await open(SIMPLE, 1400, 900);

    await page.goto(`${base}${GOVERNANCE}/view/overview`);
    await page.locator("uui-table-row").first().waitFor();
    await page.waitForTimeout(1500);
    await page.screenshot({ path: save("overview.png") });

    await page.goto(`${base}${GOVERNANCE}/view/people`);
    await page.locator("uui-table-row").first().waitFor();
    await page.waitForTimeout(1000);
    await page.screenshot({ path: save("people.png") });

    await page.setViewportSize({ width: 1400, height: 1500 });
    await openPerson(page, base, "Priya Shah");
    await shootModal(page, "default-person.png");
    await page.setViewportSize({ width: 1400, height: 900 });

    await page.setViewportSize({ width: 1400, height: 1150 });
    await page.goto(await imageUrl(call, base, "Lecture theatre"));
    await page.locator("umb-media-governance-image-view").waitFor();
    await page.waitForTimeout(2500);
    await page.screenshot({ path: save("default-image.png") });
    await page.setViewportSize({ width: 1400, height: 900 });

    await page.goto(`${base}${GOVERNANCE}/view/settings`);
    await page.locator("umb-media-governance-settings uui-input").first().waitFor();
    await page.waitForTimeout(1000);
    await page.evaluate(() => document.querySelector("umb-media-governance-settings")?.scrollIntoView());
    await page.screenshot({ path: save("settings.png"), fullPage: true });
    await page.context().close();
}

// ------------------------------------------------------------------ detailed: the optional consent detail on
{
    const { page, call, base } = await open(DETAILED, 1400, 900);
    const people = (await call("GET", "people?take=100")).items;
    const verna = people.find((p) => p.name === "Verna Burke");
    const priya = people.find((p) => p.name === "Priya Shah");

    // Verna: a realistic consent, with evidence on file.
    await call("PUT", `people/${verna.id}/consent`, { given: true, givenOn: "2025-09-12", givenBy: "Self", expiresOn: "2027-09-12", permittedUses: ["Web", "Print"] });
    await page.setViewportSize({ width: 1400, height: 2100 });
    await openPerson(page, base, "Verna Burke");
    await page.locator('umb-media-governance-person-modal input[type="file"]').setInputFiles({ name: "signed-consent-form.pdf", mimeType: "application/pdf", buffer: PDF });
    await page.locator("umb-media-governance-person-modal uui-ref-node").waitFor();
    await page.locator("umb-media-governance-person-modal .message").waitFor();
    await page.waitForTimeout(7000);   // let the "uploaded" message clear itself
    await shootModal(page, "detailed-person.png");
    await page.setViewportSize({ width: 1400, height: 900 });

    // The image: what everyone pictured has agreed to, and a consent that has run out.
    await call("PUT", `people/${priya.id}/consent`, { given: true, givenOn: "2024-03-01", givenBy: "Self", expiresOn: "2025-03-01", permittedUses: ["Web"] });
    await page.setViewportSize({ width: 1400, height: 1250 });
    await page.goto(await imageUrl(call, base, "Open day crowd"));
    await page.locator("umb-media-governance-image-view uui-tag.person").first().waitFor();
    await page.waitForTimeout(2500);
    await page.screenshot({ path: save("detailed-image.png") });
    await page.setViewportSize({ width: 1400, height: 900 });
    // Put Priya back as she was seeded.
    await call("PUT", `people/${priya.id}/consent`, { given: true, givenOn: new Date().toISOString().slice(0, 10), givenBy: "Self", expiresOn: null, permittedUses: ["Web", "Print", "Social"] });


    // The admin section of Settings (admin settings are on in this copy of the test site).
    await page.setViewportSize({ width: 1400, height: 2000 });
    await page.goto(`${base}${GOVERNANCE}/view/settings`);
    await page.locator("umb-media-governance-settings .admin").waitFor();
    await page.waitForTimeout(1500);
    await page.locator("umb-media-governance-settings .admin").screenshot({ path: save("admin-settings.png") });

    // A change marked "Changed here", and the confirmation shown before a protection is switched off.
    const admin = page.locator("umb-media-governance-settings .admin");
    const original = (await call("GET", "settings")).adminSettings.current;
    try {
        await admin.locator('uui-toggle[label="Coloured image icons"]').click();
        await admin.locator('uui-toggle[label="Block withheld image files"]').click();
        await admin.getByText("Changed here").first().waitFor();
        await page.waitForTimeout(500);
        await admin.locator("uui-box", { hasText: "Your website" }).first().screenshot({ path: save("admin-settings-changed.png") });

        await page.setViewportSize({ width: 1400, height: 900 });
        await admin.locator('uui-button[label="Save admin settings"]').click();
        await page.getByText("Switch off a protection?").waitFor();
        await page.waitForTimeout(800);
        await page.screenshot({ path: save("admin-settings-confirm.png") });
        await page.getByRole("button", { name: "Cancel" }).last().click();
    } finally {
        await call("PUT", "settings/admin", original);
    }
    await page.context().close();
}

await browser.close();
console.log("Screenshots written to " + OUT);
