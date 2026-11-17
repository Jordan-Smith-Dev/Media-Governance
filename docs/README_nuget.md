# Media Governance for Umbraco

Know which images in your media library you are allowed to use. Media Governance adds a Governance page to the Media section, records who is pictured and whether they have given consent, and flags images that need attention before they expire.

It has **no dependency on Umbraco UI Builder** or any other paid package.

## Features

- **A Governance page** in the Media section, opened from "Governance" in the sidebar and laid out like Umbraco's Log Viewer. Its tabs are **Overview** (filter tiles for All images, Needs tagging, No consent, Expiring soon and Expired, a searchable, sortable table, and CSV export), **People**, **Pages affected** and **Settings**.
- **Optional status indicators in the media tree** (signs and coloured icons), off by default so the tree looks like a normal Umbraco one. Turn them on in `appsettings.json`.
- **Governance tab on every image**: date taken, date obtained, **Expiry** (worked out from the retention period, with a red *Expired* tag once it has passed, and overridable with your own date), and the people pictured. Each tagged person is green (consent given) or red (no consent) and links to their record.
- **Consent is a yes or no on the person**, set with a checkbox on their record, so one change applies to every image they are in. Every change is audited with who made it. For clients who need more, optional settings (all off by default) add the date consent was given and who gave it, an expiry, permitted uses (with "cleared for" tags on images), and evidence upload to a private folder.
- **People repository**: a list with search, a slide-out editor, and the images each person is tagged in. People can be linked to an Umbraco member and/or user.
- **Subject access and deletion on request**: download everything held about a person as a zip, or permanently delete their details, consent and evidence in one action. The audit log keeps an anonymised entry only.
- **Optional folder markers** (red or amber) when a folder contains images that cannot be used or need attention, also off by default.
- **Select rows and export just those** from the overview. The Export button exports the ticked images, or everything matching the current filter when none are ticked.
- **Bulk actions on ticked images:** mark them as having no people (images that already have someone tagged are left alone), or set or remove an expiry date on all of them. Each image is audited.
- **Retention rules**: retention (3 years from date taken) and a warning window (90 days), set in `appsettings.json` and overridable in the CMS.
- **Daily email digest** to the addresses and user groups you choose when images enter *Expiring soon*, *Expired* or *No consent*.
- **Restricted to the user groups you choose**, defaulting to *Sensitive data*.
- **Scales to large libraries:** the overview, the People list and a person's images all load a page at a time, the tagging picker searches as you type, and long lists collapse into a "+N" with a tooltip.

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

`HideWithheldInPages` in `appsettings.json` switches this behaviour for the whole site (it is read-only in the CMS). When it is off, the tag helper renders its content as normal (and still writes the log warning, so you can find the pages). `IMediaGovernanceUsage` always reports the true status.

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

- On by default. Set `Umbraco:Community:MediaGovernance:BlockWithheldMediaFiles` to `false` in `appsettings.json` to turn it off. The Settings tab shows the current value, read-only.
- It only applies to images that have governance data (tagged people or dates). A newly installed site starts with everything allowed, and files become blocked as images are tagged and consent is missing or expires.
- Each blocked request writes a log warning (de-duplicated), the same as the tag helper.
- Status is cached for up to 30 seconds. Changes made in the backoffice apply immediately on the server where they were made; other servers in a load-balanced setup can lag by up to 30 seconds.
- A 404 stops new requests. It cannot recall copies already downloaded, and search engines drop listings over time. If a CDN sits in front of your site, purge its cache for the affected files.
- It covers files under `/media`. Media served through other routes (for example the Delivery API) is not covered.

## Where retention starts

Retention is counted from the first of these that is set: **date taken**, **date obtained**, then the image's **upload date**. So an image nobody has dated still expires (3 years after it was uploaded by default), and an older image with no dates is judged by when it was uploaded. Setting a date taken always wins. The Governance tab shows which date is being used.

> **Before installing on a site with many old images:** images with no dates and an upload date older than the retention period will show as *Expired*, be left out by the tag helper, and (unless you turn it off) have their files blocked. Tag and date the images that matter first, or set a longer retention.

## Daily email

The digest email lists images that have newly become **Expired**, **No consent**, **Needs tagging** or **Expiring soon**, grouped by status and capped at 50 per group. On a first run, every existing image that needs tagging is included, so the first email can be long.

## Status rules

Each image gets one status, first match wins:

1. **Expired**: the effective expiry date is today or earlier.
2. **No consent**: a tagged person has not given consent.
3. **Needs tagging**: nobody is tagged and the image is not marked "contains no people".
4. **Expiring soon**: expiry falls within the warning window.
5. **Compliant**.

The effective expiry is the image's own expiry date if one was chosen, otherwise date taken (or date obtained, or upload date) plus the retention period. Because it is worked out each time, changing the retention period re-dates every image that has no expiry of its own.

## Requirements

- Umbraco 17.8.1 or later (`[17.8.1, 18.0.0)`), .NET 10.
- Only media items of type **Image** are governed.

## Installation

```
dotnet add package Umbraco.Community.MediaGovernance
```

The package creates its own tables (`mgPerson`, `mgImage`, `mgImagePerson`, `mgAudit`, `mgNotified`, `mgMediaFile`) automatically on startup.

## Notes

- The overview can be sorted by any date, name, folder or status column. The status, search and sort are kept in the address, so a view can be bookmarked or shared.
- Notifications use Umbraco's configured email settings and run on the scheduling server only.
- Settings start in `appsettings.json`. In the CMS only the notification recipients, retention period and warning window can be changed; everything else is shown read-only. See the repository README for the full list.

## Known limitations

- **By default consent is only a yes or no.** Unless you switch on the optional detail, the package does not store what was agreed, when, for how long, or a copy of the form. The audit log records who changed a consent and when.
- **Evidence storage.** Evidence is kept in a private folder of the media file system, which the package never serves. If your media is stored in a blob store or served from a CDN, set `EvidenceStoragePath` to a folder on disk instead.
- **Signs on cards in the media grid are experimental.** The tree signs, coloured icons and folder markers are off by default. In the tree they work, but the card wrapper used for the grid view did not display cards when we registered it, so it is only added when `ShowStatusSigns` or `ShowFolderMarkers` is turned on, and that path has not been fixed or tested. Leave them off unless you are prepared to check the grid.
- **Access is by user group.** Anyone in a configured group sees governance data for all images, including people's names and consent. Media start nodes are not applied.

## Roadmap

The next phase adds front-end access control for media (member groups, an email gate, a secure download endpoint that can also block expired files, download tracking) and optional watermarking of served images.

## License

MIT
