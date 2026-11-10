# Browser tests

Playwright tests that drive the test site in Chrome: viewing and downloading consent evidence, refusing a file that is not what it claims to be, and checking that action buttons share widths.

```
cd tests-e2e
npm install
npm test
```

- Uses your installed Chrome, so no browser download is needed.
- Starts the test site on https://localhost:44399 if it is not already running (the first run seeds it).
- Signs in with the test site's development login from `appsettings.Development.json`. Override with `MG_EMAIL` and `MG_PASSWORD`, or point at another site with `MG_URL`.
- Needs the seeded images, such as "Campus aerial", which has a consent with evidence on file.
- Failures save a screenshot and trace under `test-results/`; `npm run report` opens the HTML report.
