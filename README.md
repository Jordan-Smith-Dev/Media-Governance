# Media Governance for Umbraco

An Umbraco 17 package that records who is pictured in each image, whether they have given consent, and when the image must stop being used. It adds a **Governance** page to the Media section, a Governance tab on every image, and stops expired or unconsented images being served.

The package's own documentation (features, status rules, front-end usage) is in [docs/README_nuget.md](docs/README_nuget.md), which is also the NuGet page. This file covers **setting up, configuring and developing** it.

## 1. Install

Requirements: Umbraco 17.8.1 or later (below 18), .NET 10, and a database Umbraco supports (SQL Server, SQLite).

```
dotnet add package Umbraco.Community.MediaGovernance
```

Run the site. On first start the package creates its own tables (`mgPerson`, `mgImage`, `mgImagePerson`, `mgAudit`, `mgNotified`, `mgMediaFile`). There is nothing to run by hand.

Then, in the backoffice:

1. Give the people who should use it a **user group**, and list that group's alias under `AllowedUserGroups` (see section 2). With nothing listed, only the *Sensitive data* group has access.
2. Open **Governance** at the top of the Media section's sidebar. Its **Settings** tab shows what is in force and lets you set the notification recipients, retention period and warning window.
3. Add people on the **People** tab and tick **Consent given** for those who have agreed. Then open any image, use its **Governance** tab to tag the people pictured, and the image shows each person in green (consent given) or red (no consent).

## 2. Who can use it

Only members of the user groups you choose can use Governance, including its API. List their aliases in `appsettings.json`:

```json
{ "Umbraco": { "Community": { "MediaGovernance": { "AllowedUserGroups": [ "sensitiveData", "editors" ] } } } }
```

If `AllowedUserGroups` is empty or missing, only the **Sensitive data** group (alias `sensitiveData`) has access, so the default is the tightest one that Umbraco already provides for personal data. Users in other groups still see the media library as before, but cannot open Governance, and receive a "no access" message if they try its endpoints. Being in the Media section is also required. Media start nodes are not applied: someone with access sees governance data for all images.

## 3. Configure

Everything starts in `appsettings.json`, under `Umbraco:Community:MediaGovernance`:

```json
{
  "Umbraco": {
    "Community": {
      "MediaGovernance": {
        "RetentionYears": 3,
        "WarningDays": 90,
        "BlockWithheldMediaFiles": true,
        "HideWithheldInPages": true,
        "ShowStatusSigns": false,
        "ColourTreeIcons": false,
        "ShowFolderMarkers": false,
        "AllowedUserGroups": [ "sensitiveData" ]
      }
    }
  }
}
```

| Setting | Default | What it does | In the CMS |
|---|---|---|---|
| `RetentionYears` | 3 | Years an image can be used, counted from date taken, then date obtained, then upload date | Can be **overridden** in Governance, Settings |
| `WarningDays` | 90 | How far ahead an image shows as *Expiring soon* | Can be **overridden** in Governance, Settings |
| Notification recipients | none | Email addresses and user groups that get the daily digest | **Edited** in Governance, Settings |
| `HideWithheldInPages` | `true` | The `<media-governance>` tag helper leaves out images that are Expired or have no consent. `false`: they still show, and are still logged | Read-only |
| `BlockWithheldMediaFiles` | `true` | Requests for the file of a withheld image return 404 to the public (signed-in editors can still see it). `false`: only pages are affected | Read-only |
| `ShowStatusSigns` | `false` | Status signs on images in the media tree and collection. Off, so the tree looks like a normal Umbraco one | Not shown |
| `ColourTreeIcons` | `false` | Colours each image's icon in the tree by status | Not shown |
| `ShowFolderMarkers` | `false` | A marker on folders that contain images needing attention | Not shown |
| `AllowedUserGroups` | `sensitiveData` | Who can use Governance (section 2) | Read-only |

An override saved in the CMS is stored only when it differs from `appsettings.json`, so a value left at its starting point keeps following the file.

**Changing the retention period affects existing images immediately, not only new ones.** Expiry dates are worked out from the retention period each time they are needed, so a new value re-dates every image that does not have its own expiry. Shortening it can make images Expired, and expired images are withheld from the website. The Settings tab warns about this before you save. Every settings change is written to the audit log with the old and new values (recipients are counted, never listed).

### Email

The daily digest uses Umbraco's normal email settings, so configure `Umbraco:CMS:Global:Smtp` (host, port, from address) as for any Umbraco site. If email is not configured, or nobody is listed (no addresses and no user groups), no digest is sent and nothing else is affected. A user group is enough on its own: everyone in it gets the email at the address on their user account (disabled and locked-out users are skipped, and nobody gets it twice). Each image in the email links straight to its Governance tab, with a link to Governance at the end. The links use the site's address, which Umbraco learns from its first request, or you can set it with `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` (recommended for scheduled emails, e.g. `https://www.example.com`). Until it is known, names appear as plain text. It runs once every 24 hours (first run two minutes after startup) on the scheduling server only.

### Before you go live on an existing site

Images with no dates and an upload date older than the retention period will show as Expired, be hidden by the tag helper and (unless you turn blocking off) have their files return 404. Either set `BlockWithheldMediaFiles` (and, if you wish, `HideWithheldInPages`) to `false` first, or review Governance, bulk-mark images as having no people, and set a longer retention before enabling it. Use the **Expired** and **Needs tagging** filters and the bulk actions to work through the backlog.

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
- **Consent is a yes or no on the person.** Nothing else about it is stored: no uses, dates, expiry or evidence. Every change, and every settings change, is written to the audit log with who made it and when. That is the history of a consent.
- **Deletion on request:** on the People tab, *Delete* permanently deletes a person's details and consent, and removes them from all images. The audit log keeps an anonymised entry only.
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

**Run the test site** (SQLite, seeded with sample images and people):

```
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Umbraco.Community.MediaGovernance.TestSite --urls "https://localhost:44399;http://localhost:5199"
```

Open `https://localhost:44399/umbraco`. The test login is in `src/Umbraco.Community.MediaGovernance.TestSite/appsettings.Development.json` (a throw-away local account; change nothing there for production). Development settings allow concurrent logins so browser tests do not sign you out, and allow the `admin` user group to use Governance.

**Try the daily email locally**

The test site's Development settings write emails to a folder instead of sending them (`Smtp.DeliveryMethod: SpecifiedPickupDirectory`, folder `umbraco/Data/TEMP/Mail` inside the test site). To see one:

1. Start the test site as above.
2. In **Governance, Settings**, add your address under *Email addresses* and save. Nothing is sent to it; it is only used as the recipient.
3. The digest only lists images that have *newly* needed attention since the last run, and the site remembers what it has already reported. To make everything count as new, stop the site and empty the `mgNotified` table in `umbraco/Data/Umbraco.sqlite.db` (any SQLite tool, for example `DELETE FROM mgNotified;`).
4. Start the site. The job runs two minutes after startup. Wait, then open the newest `.eml` file in the folder above (double-click it, it opens in Outlook or Mail, or open it in any text editor).
5. Check the links. They use `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` (set to `https://localhost:44399` for the test site), so they open the image's Governance tab. Remove that setting to see the plain-text fallback.

To send through a real mail server instead, replace the `Smtp` block in `appsettings.Development.json` with your server details (host, port, username, password, `From`). Use a throw-away mailbox, and do not commit credentials.

If no file appears: check that a recipient is saved, that `mgNotified` was emptied, that some image is Expired, No consent, Needs tagging or Expiring soon, and look for warnings in the site log.

**Pack for NuGet / the Umbraco Marketplace.** The backoffice files are built, not committed, so build them before packing:

```
cd src/Umbraco.Community.MediaGovernance/Client && npm ci && npm run build
cd ../../.. && dotnet pack src/Umbraco.Community.MediaGovernance/Umbraco.Community.MediaGovernance.csproj -c Release -o artifacts
```

The `.nupkg` contains the package and Core assemblies, the built `App_Plugins` files and `docs/README_nuget.md`. The Marketplace listing comes from `umbraco-marketplace.json` and the `umbraco-marketplace` NuGet tag.

**Tests**

```
dotnet test src/Umbraco.Community.MediaGovernance.sln     # unit tests
cd tests-e2e && npm install && npm test                   # browser tests; starts the test site on a database of its own
```

**Pack**

```
cd src/Umbraco.Community.MediaGovernance/Client && npm run build
dotnet pack src/Umbraco.Community.MediaGovernance -c Release
```

See [CHANGELOG.md](CHANGELOG.md) for what changed in each version.

## License

MIT
