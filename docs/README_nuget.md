# Media Governance for Umbraco

Know which images in your media library you are allowed to use. Media Governance adds a compliance dashboard to the Media section, records who is pictured and whether they consented, and flags images that need attention before they expire.

It has **no dependency on Umbraco UI Builder** or any other paid package.

## Features

- **Media governance dashboard** in the Media section: filter tiles (All images, Needs tagging, No consent, Expiring soon, Expired), a searchable table, and CSV export.
- **Status indicators in the media tree** so editors see problems where they work.
- **Governance tab on every image**: date taken, date obtained, custom expiry, people pictured, and a full consent history per person.
- **Full consent records**: who gave consent, when, permitted uses (Web, Print, Social, Other by default), optional expiry, withdrawal, and an uploaded evidence file (PDF/PNG/JPG, 10 MB).
- **People repository**: people can be linked to an Umbraco member and/or user.
- **Erasure on request**: permanently delete a person's details, consent records and evidence in one action. The audit log keeps an anonymised entry only.
- **Folders show a marker** (red or amber) when they contain images that cannot be used or need attention, so you can find problems without opening every folder.
- **Select rows and export just those** from the dashboard overview. The Export button exports the ticked images, or everything matching the current filter when none are ticked.
- **Bulk actions on ticked images:** mark them as having no people (images that already have someone tagged are left alone), or set or remove a custom expiry date on all of them. Each image is audited.
- **Retention rules**: default retention (3 years from date taken) and a warning window (90 days), both configurable.
- **Daily email digest** to the addresses you choose when images enter *Expiring soon*, *Expired* or *No consent*.

## Using it on your website

The dashboard only *flags* images. To stop an expired image appearing on a page, ask the package from your templates.

**Tag helper.** Add this once to `Views/_ViewImports.cshtml`:

```
@addTagHelper *, Umbraco.Community.MediaGovernance
```

Then wrap any markup that shows a media item. Nothing is rendered when the image has expired or a tagged person has not consented, and no wrapper element is added to the page:

```html
<media-governance media="@Model.Image">
    <img src="@Model.Image.Url()" alt="@Model.Image.Name" />
</media-governance>

<!-- optional: show something else instead -->
<media-governance media="@Model.Image" show="Unusable">
    <p>This image is no longer available.</p>
</media-governance>
```

Use `key="@someGuid"` instead of `media` when you only have the media key.

The Settings tab's **Hide withheld images in pages** switches this behaviour for the whole site. When it is off, the tag helper renders its content as normal (and still writes the log warning, so you can find the pages). `IMediaGovernanceUsage` always reports the true status.

**Service.** For logic in controllers or views, inject `IMediaGovernanceUsage`:

```csharp
bool ok = await governance.IsUsableAsync(mediaKey);
GovernanceStatus status = await governance.GetStatusAsync(mediaKey);               // for finer control
IReadOnlyDictionary<Guid, bool> map = await governance.GetUsableAsync(keys);       // one lookup for a gallery
```

"Usable" means *not Expired and not No consent*. *Expiring soon* and *Needs tagging* are still usable. Media that has never been tagged counts as *Needs tagging*, so it stays usable. The service returns only a status or yes/no, never names or consent details.

**Finding pages that still use withheld images.** Each time the tag helper withholds an image, a warning is written to the Umbraco log naming the status, the media key and the page path, for example `Media governance withheld Expired media <key> on page /news/article`. A given page and image is logged at most once every six hours, so busy pages do not flood the log. Only keys, statuses and paths are logged, never names or consent details.

Limitations:
- The helper only covers markup you wrap with it. **Images embedded in a rich text editor are stored as plain HTML, so they are neither hidden nor logged.** Umbraco's own *Referenced by* list on the media item does include them, so check it before removing an image.
- The helper hides the image **in your pages**. The file itself is protected separately, see below.

## Blocking the file itself

Hiding an image in a page does not stop someone who already has its URL, or finds it through a search engine. So the package also stops **withheld** images being served: a request for the file of an Expired image, or an image of someone who has not consented, returns **404** to everyone except signed-in backoffice users (so editors can still see the image to fix it). Images that are not withheld are untouched, including resized (`?width=…`) requests.

- On by default. Editors can turn it off with **Block withheld image files** in the dashboard's Settings tab (the *Front end* box). `Umbraco:Community:MediaGovernance:BlockWithheldMediaFiles` in `appsettings.json` sets the starting value; a choice saved in the Settings tab wins.
- It only applies to images that have governance data (tagged people or dates). A newly installed site starts with everything allowed, and files become blocked as images are tagged and consent is missing or expires.
- Each blocked request writes a log warning (de-duplicated), the same as the tag helper.
- Status is cached for up to 30 seconds. Changes made in the dashboard apply immediately on the server where they were made; other servers in a load-balanced setup can lag by up to 30 seconds.
- A 404 stops new requests. It cannot recall copies already downloaded, and search engines drop listings over time. If a CDN sits in front of your site, purge its cache for the affected files.
- It covers files under `/media`. Media served through other routes (for example the Delivery API) is not covered.

## Where retention starts

Retention is counted from the first of these that is set: **date taken**, **date obtained**, then the image's **upload date**. So an image nobody has dated still expires (3 years after it was uploaded by default), and an older image with no dates is judged by when it was uploaded. Setting a date taken always wins. The Governance tab shows which date is being used.

> **Before installing on a site with many old images:** images with no dates and an upload date older than the retention period will show as *Expired*, be left out by the tag helper, and (unless you turn it off) have their files blocked. Tag and date the images that matter first, or set a longer default retention in Settings.

## Daily email

The digest email lists images that have newly become **Expired**, **No consent**, **Needs tagging** or **Expiring soon**, grouped by status and capped at 50 per group. On a first run, every existing image that needs tagging is included, so the first email can be long.

## Status rules

Each image gets one status, first match wins:

1. **Expired**: the effective expiry date is today or earlier.
2. **No consent**: a tagged person has no active consent.
3. **Needs tagging**: nobody is tagged and the image is not marked "contains no people".
4. **Expiring soon**: expiry falls within the warning window.
5. **Compliant**.

The effective expiry is the custom expiry if set, otherwise date taken (or date obtained) plus the retention period, and never later than a tagged person's consent expiry.

## Requirements

- Umbraco 17.8.1 or later (`[17.8.1, 18.0.0)`), .NET 10.
- Only media items of type **Image** are governed.

## Installation

```
dotnet add package Umbraco.Community.MediaGovernance
```

The package creates its own tables (`mgPerson`, `mgImage`, `mgImagePerson`, `mgConsent`, `mgAudit`, `mgNotified`) automatically on startup.

## Notes

- Evidence files are stored under `umbraco/Data/MediaGovernance/evidence` (outside `wwwroot`, never served directly).
- Evidence can be viewed, downloaded, replaced or removed from an image's *Governance* tab, which also shows its type and size. Uploads are checked against the PDF, PNG and JPG file signatures, not just the extension. Each upload, view and removal is recorded in the audit log, and the *History* link shows who did what and when.
- The overview can be sorted by any date, name, folder or status column. The status, search and sort are kept in the address, so a view can be bookmarked or shared.
- Notifications use Umbraco's configured email settings and run on the scheduling server only.
- The People list lives in the dashboard's *People* tab.

## Known limitations

- **Evidence storage is local disk.** Files live under `umbraco/Data/MediaGovernance/evidence` on the server that received them. That is fine for a single server. On load-balanced or scaled-out hosts, or anywhere the disk is not persistent (containers, some cloud plans), point that folder at shared, persistent storage, otherwise evidence can go missing and erasure on one server cannot remove files held by another. A blob storage option is planned.
- **Access follows Media section access.** Any user with access to the Media section can see governance data for all images, including people's names and consent records. Media start nodes are not applied.

## Roadmap

The next phase adds front-end access control for media (member groups, an email gate, a secure download endpoint that can also block expired files, download tracking) and optional watermarking of served images.

## License

MIT
