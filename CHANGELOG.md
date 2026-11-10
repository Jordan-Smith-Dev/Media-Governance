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
- The daily email now also covers images that need tagging.
- Media folders show a red or amber marker when they contain images that cannot be used or need attention. Image icons in the tree are coloured by status.
- Dashboard: row selection with "Export selected", consent shown as solid green and red pills, "No people" and "Not tagged yet" wording.
- Front-end helpers: a `<media-governance>` tag helper and an `IMediaGovernanceUsage` service so templates can leave out expired images and images without consent. Withheld images are written to the Umbraco log as warnings (de-duplicated) with the page path, so affected pages can be found.

Differences from the design prototype:

- People are managed in the dashboard's *People* tab instead of a separate "Repositories" section.
- EXIF prefill of *Date taken* is not included yet.
- Notification recipients are email addresses (no user-group picker).
