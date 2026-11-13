# Media Governance for Umbraco

An Umbraco 17 package that records who is pictured in each image, whether they consented, and when the image must stop being used. It adds a compliance dashboard to the Media section, a Governance tab on every image, and stops expired or unconsented images being served.

The package's own documentation (features, status rules, front-end usage) is in [docs/README_nuget.md](docs/README_nuget.md), which is also the NuGet page. This file covers **setting up, configuring and developing** it.

## 1. Install

Requirements: Umbraco 17.8.1 or later (below 18), .NET 10, and a database Umbraco supports (SQL Server, SQLite).

```
dotnet add package Umbraco.Community.MediaGovernance
```

Run the site. On first start the package creates its own tables (`mgPerson`, `mgImage`, `mgImagePerson`, `mgConsent`, `mgAudit`, `mgNotified`). There is nothing to run by hand.

Then, in the backoffice:

1. Open the **Media** section. The **Media governance** dashboard is there.
2. Open the dashboard's **Settings** tab and set the retention period, warning window and email recipients (see below).
3. Add people on the **People** tab, then open any image and use its **Governance** tab to tag them and record consent.

## 2. Who can use it

Access follows Umbraco's **Media section** permission. Any user who can open the Media section can see governance data for all images, including names and consent records. Media start nodes are not applied. Give the Media section only to users who are allowed to see this information.

## 3. Configure

### In the dashboard (Settings tab)

| Setting | Default | What it does |
|---|---|---|
| Retention (years) | 3 | Years an image can be used, counted from date taken, then date obtained, then upload date |
| Warning window (days) | 90 | How far ahead an image shows as *Expiring soon* |
| Permitted uses | Web, Print, Social, Marketing, Other | The list offered when recording a consent |
| Notification emails | none | Who receives the daily digest |

### In `appsettings.json`

Only one option, under `Umbraco:Community:MediaGovernance`:

```json
{
  "Umbraco": {
    "Community": {
      "MediaGovernance": {
        "BlockWithheldMediaFiles": true
      }
    }
  }
}
```

`BlockWithheldMediaFiles` (default `true`): a request for the file of an Expired image, or an image of someone who has not consented, returns 404 to everyone except signed-in backoffice users. Set `false` to only flag images and not block files.

### Email

The daily digest uses Umbraco's normal email settings, so configure `Umbraco:CMS:Global:Smtp` (host, port, from address) as for any Umbraco site. If email is not configured, or no recipients are set, no digest is sent and nothing else is affected. Each image in the email links straight to its Governance tab, with a link to the dashboard at the end. The links use the site's address, which Umbraco learns from its first request, or you can set it with `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` (recommended for scheduled emails, e.g. `https://www.example.com`). Until it is known, names appear as plain text. It runs once every 24 hours (first run two minutes after startup) on the scheduling server only.

### Evidence files

Consent evidence (PDF, PNG, JPG, up to 10 MB) is stored on disk at `umbraco/Data/MediaGovernance/evidence`, outside `wwwroot`, and is only served through the authenticated API. On load-balanced or container hosts, point that folder at persistent shared storage and include it in backups. Blob storage is planned.

### Before you go live on an existing site

Images with no dates and an upload date older than the retention period will show as Expired, be hidden by the tag helper and (unless you turn blocking off) have their files return 404. Either set `BlockWithheldMediaFiles` to `false` first, or review the dashboard, bulk-mark images as having no people, and set a longer retention before enabling it. Use the **Expired** and **Needs tagging** filters and the bulk actions to work through the backlog.

## 4. Use it on the website

Add the tag helper once to `Views/_ViewImports.cshtml`:

```
@addTagHelper *, Umbraco.Community.MediaGovernance
```

and wrap markup that shows a media item:

```html
<media-governance media="@Model.Image">
    <img src="@Model.Image.Url()" alt="@Model.Image.Name" />
</media-governance>
```

For code, inject `IMediaGovernanceUsage` and call `IsUsableAsync(mediaKey)`. Details and limitations (rich text images, CDNs) are in [docs/README_nuget.md](docs/README_nuget.md).

## 5. Privacy

- The audit log (`mgAudit`) records who changed what and when.
- **Erasure:** on the People tab, *Erase* permanently deletes a person's details, consent records and evidence files. The audit log keeps an anonymised entry only.
- Logs and the front-end service only ever contain media keys, statuses and page paths, never names or consent details.

## Develop

```
src/Umbraco.Community.MediaGovernance        the package (C#, plus Client/ for the backoffice UI)
src/Umbraco.Community.MediaGovernance.Core   status rules, no Umbraco dependency
src/Umbraco.Community.MediaGovernance.Tests  unit tests
src/Umbraco.Community.MediaGovernance.TestSite  Umbraco site with seed data
tests-e2e                                    Playwright browser tests
```

**Build the backoffice client** (output goes to the package's `wwwroot`, which is not committed):

```
cd src/Umbraco.Community.MediaGovernance/Client
npm install
npm run build        # or: npm run watch
```

**Run the test site** (SQLite, seeded with sample images, people and consents):

```
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Umbraco.Community.MediaGovernance.TestSite --urls "https://localhost:44399;http://localhost:5199"
```

Open `https://localhost:44399/umbraco`. The test login is in `src/Umbraco.Community.MediaGovernance.TestSite/appsettings.Development.json` (a throw-away local account; change nothing there for production). Development settings allow concurrent logins so browser tests do not sign you out.

**Try the daily email locally**

The test site's Development settings write emails to a folder instead of sending them (`Smtp.DeliveryMethod: SpecifiedPickupDirectory`, folder `umbraco/Data/TEMP/Mail` inside the test site). To see one:

1. Start the test site as above.
2. In the dashboard's **Settings** tab, add your address under *Notification emails* and save. Nothing is sent to it; it is only used as the recipient.
3. The digest only lists images that have *newly* needed attention since the last run, and the site remembers what it has already reported. To make everything count as new, stop the site and empty the `mgNotified` table in `umbraco/Data/Umbraco.sqlite.db` (any SQLite tool, for example `DELETE FROM mgNotified;`).
4. Start the site. The job runs two minutes after startup. Wait, then open the newest `.eml` file in the folder above (double-click it, it opens in Outlook or Mail, or open it in any text editor).
5. Check the links. They use `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` (set to `https://localhost:44399` for the test site), so they open the image's Governance tab. Remove that setting to see the plain-text fallback.

To send through a real mail server instead, replace the `Smtp` block in `appsettings.Development.json` with your server details (host, port, username, password, `From`). Use a throw-away mailbox, and do not commit credentials.

If no file appears: check that a recipient is saved, that `mgNotified` was emptied, that some image is Expired, No consent, Needs tagging or Expiring soon, and look for warnings in the site log.

**Tests**

```
dotnet test src/Umbraco.Community.MediaGovernance.sln     # unit tests
cd tests-e2e && npm install && npm test                   # browser tests; the test site must be running
```

**Pack**

```
cd src/Umbraco.Community.MediaGovernance/Client && npm run build
dotnet pack src/Umbraco.Community.MediaGovernance -c Release
```

See [CHANGELOG.md](CHANGELOG.md) for what changed in each version.

## License

MIT
