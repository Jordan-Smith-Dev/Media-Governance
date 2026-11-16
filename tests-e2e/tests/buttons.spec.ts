import { expect, test, type Page } from "@playwright/test";
import { login, view } from "./helpers";

/** Widths of every action button on the page, grouped by size (compact or full). */
async function widths(page: Page) {
    return page.evaluate(() => {
        const found: { compact: boolean; label: string; width: number }[] = [];
        const walk = (root: Document | ShadowRoot) => {
            root.querySelectorAll("uui-button.action").forEach((b) => {
                const r = b.getBoundingClientRect();
                if (r.width > 0) found.push({ compact: b.hasAttribute("compact"), label: b.getAttribute("label") ?? "", width: Math.round(r.width) });
            });
            root.querySelectorAll("*").forEach((el) => el.shadowRoot && walk(el.shadowRoot));
        };
        walk(document);
        return found;
    });
}

function expectConsistent(found: { compact: boolean; label: string; width: number }[]) {
    expect(found.length).toBeGreaterThan(0);
    for (const compact of [true, false]) {
        const group = found.filter((f) => f.compact === compact);
        if (group.length < 2) continue;
        const min = Math.min(...group.map((g) => g.width));
        const max = Math.max(...group.map((g) => g.width));
        expect(max - min, `${compact ? "compact" : "full-size"} buttons differ: ${JSON.stringify(group)}`).toBeLessThanOrEqual(1);
    }
}

test.beforeEach(async ({ page }) => {
    await login(page);
});

for (const tab of ["overview", "people", "settings"] as const) {
    test(`Action buttons share widths on the ${tab} tab`, async ({ page }) => {
        await page.goto(view(tab));
        await page.locator("uui-button.action").first().waitFor();
        expectConsistent(await widths(page));
    });
}

test("Action buttons share widths in the person modal", async ({ page }) => {
    await page.goto(view("people"));
    await page.getByRole("button", { name: "Tom Reyes" }).first().click();
    await page.locator("umb-media-governance-person-modal uui-button.action").first().waitFor();
    expectConsistent(await widths(page));
});
