# Changelog

## 0.1.0 (unreleased)

First version: Phase 1, media compliance.

- Media section dashboard with status tiles, filter, paging and CSV export.
- Governance tab on image media items; status indicators in the media tree.
- People repository (optionally linked to members/users) with permanent erasure.
- Per-image consent records with evidence upload, withdrawal and history.
- Configurable retention and warning window; daily email digest.
- Withheld media files (expired, or without consent) return 404 to the public, so knowing or finding the URL is not enough. Signed-in backoffice users are exempt. On by default; `Umbraco:Community:MediaGovernance:BlockWithheldMediaFiles` turns it off.
- Retention starts from date taken, then date obtained, then the upload date, so undated and older images still expire.
- The daily email now also covers images that need tagging, and each image links to its Governance tab (plus a link to the dashboard) when the site's address is known.
- Media folders show a red or amber marker when they contain images that cannot be used or need attention. Image icons in the tree are coloured by status.
- Dashboard: row selection, consent shown as solid green and red pills, "No people" and "Not tagged yet" wording.
- One Export button: exports the ticked images, or everything matching the current filter when none are ticked.
- Overview sorting by any column, with search, filter and sort kept in the address so a view can be shared. Column headings stay in view while scrolling. Status tooltips explain each status, and the "+N" chips list the hidden names and issues.
- Bulk actions on ticked images (up to 500): mark as no people (images with someone tagged are skipped), set or remove a custom expiry. Each image is audited.
- Evidence: view, download, replace and remove from the Governance tab; type and size shown; uploads checked against PDF/PNG/JPG signatures; a history of who uploaded, viewed or removed it.
- A notice on an image when a tagged person has no consent in force, with a shortcut to add one. Tagging is not blocked.
- Pages affected tab, changes saved immediately, a fixed "Given by" list, settings validation and a custom calendar.
- Icons on action buttons; Remove is red. Dark mode checked.
- 239 unit tests and 28 browser tests.
- Settings tab: a *Front end* box with two switches, *Hide withheld images in pages* (the tag helper) and *Block withheld image files* (404 for the file). The appsettings option is now only the starting value for file blocking.
- Front-end helpers: a `<media-governance>` tag helper and an `IMediaGovernanceUsage` service so templates can leave out expired images and images without consent. Withheld images are written to the Umbraco log as warnings (de-duplicated) with the page path, so affected pages can be found.

Differences from the design prototype:

- People are managed in the dashboard's *People* tab instead of a separate "Repositories" section.
- EXIF prefill of *Date taken* is not included yet.
- Notification recipients are email addresses (no user-group picker).
