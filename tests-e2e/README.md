# Browser tests

Playwright tests that drive the test site in Chrome: the Governance page and its tabs, people and consent, an image's Expiry and tagged people, the bulk actions, the settings screen, dark mode, and visual checks (button and pill widths, padding).

```
cd tests-e2e
npm install
npm test
```

- Uses your installed Chrome, so no browser download is needed.
- Starts the test site on https://localhost:44399 if it is not already running. It uses a database of its own, `umbraco/Data/Umbraco.e2e-2.sqlite.db` in the test site, seeded on the first run. When the seed data changes, change the file name in `playwright.config.ts` (for example to `e2e-3`) so a fresh one is made.
- Signs in with the test site's development login from `appsettings.Development.json`. Override with `MG_EMAIL` and `MG_PASSWORD`, or point at another site with `MG_URL`.
- Needs the seeded images and people, such as "Campus aerial" (expired) and "Tom Reyes" (no consent). The tests put back anything they change.
- Failures save a screenshot and trace under `test-results/`; `npm run report` opens the HTML report.
