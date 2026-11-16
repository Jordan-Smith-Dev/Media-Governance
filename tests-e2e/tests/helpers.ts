import { expect, type Page } from "@playwright/test";

// The test site's development login (appsettings.Development.json). Never a real account.
const EMAIL = process.env.MG_EMAIL ?? "admin@example.com";
const PASSWORD = process.env.MG_PASSWORD ?? "1234567890";

const API = "/umbraco/umbracocommunitymediagovernance/api/v1";

/** The Governance page, opened from "Governance" in the Media sidebar. Each tab is a view under it. */
export const GOVERNANCE = "/umbraco/section/media/workspace/media-governance";
export const view = (name: "overview" | "people" | "pages" | "settings") => `${GOVERNANCE}/view/${name}`;

// The backoffice's own bearer token, taken from its requests (it is short-lived, so keep the latest).
const tokens = new WeakMap<Page, { value?: string }>();

export async function login(page: Page) {
    const holder: { value?: string } = {};
    tokens.set(page, holder);
    page.on("request", (r) => {
        const auth = r.headers()["authorization"];
        if (auth?.startsWith("Bearer ")) holder.value = auth;
    });

    await page.goto("/umbraco");
    await page.getByLabel(/e-?mail/i).first().fill(EMAIL);
    await page.locator("#password-input").fill(PASSWORD);
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(/\/umbraco\/section\//, { timeout: 30_000 });
    await expect.poll(() => holder.value, { timeout: 30_000 }).toBeTruthy();
}

/** Finds a seeded image by name using the package's own overview API. */
async function mediaKeyFor(page: Page, name: string): Promise<string> {
    const auth = tokens.get(page)?.value;
    const res = await page.request.get(`${API}/overview?take=500`, { headers: { Authorization: auth ?? "" } });
    expect(res.ok(), `overview API returned ${res.status()}`).toBeTruthy();
    const body = (await res.json()) as { items: { mediaKey: string; name: string }[] };
    const item = body.items.find((i) => i.name === name);
    expect(item, `seeded image "${name}" not found`).toBeTruthy();
    return item!.mediaKey;
}

/** Opens a seeded image's Governance tab directly by URL. */
export async function openGovernance(page: Page, image: string) {
    const key = await mediaKeyFor(page, image);
    await page.goto(`/umbraco/section/media/workspace/media/edit/${key}/invariant/view/governance`);
    await expect(page.locator("umb-media-governance-image-view")).toBeVisible({ timeout: 30_000 });
}

/** Calls the package's own API with the backoffice's bearer token. */
export async function api<T = unknown>(page: Page, method: "GET" | "PUT" | "POST" | "DELETE", path: string, body?: unknown): Promise<T> {
    const auth = tokens.get(page)?.value ?? "";
    const res = await page.request.fetch(`${API}/${path}`, { method, headers: { Authorization: auth }, data: body });
    expect(res.ok(), `${method} ${path} returned ${res.status()}`).toBeTruthy();
    const text = await res.text();
    return (text ? JSON.parse(text) : undefined) as T;
}

type ImageDetails = { dateTaken: string | null; dateObtained: string | null; customExpiry: string | null; noPeople: boolean; taggedPersonIds: string[] };

/** Records an image's details so a test that changes them can put them back. */
export async function snapshotImage(page: Page, mediaKey: string) {
    const d = await api<ImageDetails>(page, "GET", `image/${mediaKey}`);
    const original: ImageDetails = { dateTaken: d.dateTaken, dateObtained: d.dateObtained, customExpiry: d.customExpiry, noPeople: d.noPeople, taggedPersonIds: d.taggedPersonIds };
    return { original, restore: () => api(page, "PUT", `image/${mediaKey}`, original) };
}

export { mediaKeyFor };

export type PersonSummary = { id: string; name: string; consentGiven: boolean };

/** Finds a seeded person by name using the package's own people API. */
export async function personFor(page: Page, name: string): Promise<PersonSummary> {
    const body = await api<{ items: PersonSummary[] }>(page, "GET", "people?take=200");
    const person = body.items.find((p) => p.name === name);
    expect(person, `seeded person "${name}" not found`).toBeTruthy();
    return person!;
}
