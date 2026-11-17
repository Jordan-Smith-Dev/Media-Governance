# Changelog

## 0.1.0 (unreleased)

First version: Phase 1, media governance.

- **Governance page** in the Media section (opened from the sidebar, laid out like the Log Viewer) with Overview, People, Pages affected and Settings tabs.
- **Overview:** filter tiles, search, sorting by any column (kept in the address so a view can be shared), column headings that stay in view, row selection, CSV export of the ticked rows or the current filter, and paging with "Load more". Consent and status pills are one width.
- **Consent is a yes or no on the person**, set with a checkbox, instead of a record per image. One change applies to every image the person is in, and every change is audited with who and when.
- **People:** list with search and paging, a slide-out editor (Umbraco's sidebar modal), the images each person is tagged in (paged), optional links to a member and/or user, and permanent deletion.
- **Governance tab on an image:** date taken (with a suggestion from the photo's camera data), date obtained, **Expiry** prefilled with the calculated date and tagged *Expired* once it has passed, and tagged people shown green (consent given) or red (no consent), linking to their record. The tagging picker searches as you type.
- **Settings start in `appsettings.json`.** In the CMS only the notification recipients, retention period and warning window can be changed; everything else is shown read-only. A warning explains that changing retention re-dates existing images. Settings changes are audited with the old and new values.
- **Access** is limited to the user groups listed under `AllowedUserGroups`, defaulting to *Sensitive data*.
- **Daily email digest** to email addresses and user groups, with each image linked to its Governance tab. Nobody listed means nothing is sent.
- **Withheld images:** expired images, and images of people without consent, are hidden from pages by the `<media-governance>` tag helper and return 404 for their file, with signed-in backoffice users exempt (`HideWithheldInPages`, `BlockWithheldMediaFiles`). A log warning names the page. `IMediaGovernanceUsage` lets templates ask.
- **Media tree:** status signs, coloured image icons and folder markers (`ShowStatusSigns`, `ColourTreeIcons`, `ShowFolderMarkers`), all **off by default** so the tree looks like a normal Umbraco one; turn them on in `appsettings.json`.
- **Retention** starts from date taken, then date obtained, then the upload date, so undated images still expire.
- **Bulk actions** on ticked images (up to 500): mark as no people, set or remove an expiry date (in an Umbraco modal). Each image is audited.
- **Pages affected:** lists the pages that use withheld images, a page at a time, with a "+N" tooltip when a page has many.
- Built with Umbraco's own components throughout (workspace, sidebar and dialog modals, `umb-input-date`, UUI tags and buttons) and checked in dark mode.

Differences from the design prototype:

- People are managed in the Governance page's *People* tab instead of a separate "Repositories" section.
- Consent was designed as a record per person and image with permitted uses, dates and an evidence file. It was reduced to a yes or no on the person. The earlier version is kept on the branch `archive/detailed-consent`.
- EXIF prefill of *Date taken* is offered as a suggestion on the image rather than filled in automatically.
