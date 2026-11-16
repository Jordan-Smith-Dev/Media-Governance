# Media Governance: Design

Date: 2026-10-29
Status: Draft, awaiting approval

## Intent and why

Media Governance is a community Umbraco package (modelled on the uRestore package at `C:\DEV-PERSONAL\uRestore`) that lets organisations govern the images in their media library: who is pictured, whether they consented to the image being used, and when the image must stop being used.

A Claude Design prototype exists (https://claude.ai/design/p/277d1fbe-8a46-43e7-99b0-d8d8a478c035, file `Media Governance.html`). It was built on the assumption of Umbraco UI Builder. This package must NOT depend on UI Builder, so it works for clients who do not have it.

A separate client brief (SRM, "Media Library / Download Portal", Carrie-Anne Wood, 23/04/2025) asks for gated, member-based access to media, download tracking, and optional watermarking. That is a different problem (access control, not consent/expiry) and is deferred to Phase 2. Phase 1 must not make Phase 2 harder.

## Goals (Phase 1)

1. A **Media section dashboard** ("Media governance") showing compliance state of all images: filter tiles (All images, Needs tagging, No consent, Expiring soon, Expired), a filterable table (name, folder, people, date taken, expiry date, consent, status), and Export.
2. **Status indicators in the media tree** (coloured dot per image by status).
3. A **People repository**: people who can be tagged in images, managed in the backoffice.
4. Per-image governance data: people pictured, date taken, optional custom expiry date, consent.
5. **Full consent records** (decided by the user): a consent is a record, not a flag. Each record holds who gave it, when, the scope/permitted uses, optional expiry or withdrawal, and optional evidence (uploaded file or reference). Consent history is retained.
6. **Settings**: default retention (years, counted from date taken or obtained, default 3) and warning window (days, default 90).
7. Computed status per image (see Status rules).
8. Scheduled expiry evaluation, so state does not depend on someone opening the dashboard. Optional notification when images enter the warning window or expire.
9. Works without UI Builder.

## Non-goals (Phase 1)

- Front-end gated access, member groups, email gate, download endpoint, download/usage tracking, watermarking (Phase 2).
- Supply-chain directory from the SRM brief (not a media feature; out of the package).
- Umbraco v13 / AngularJS backoffice support.
- Automatically removing or unpublishing expired images (flag only, in Phase 1).

## Anti-goals

- Hard dependency on UI Builder or any other paid/third-party package.
- Storing governance data in a way that cannot be audited (no consent history, no record of who changed what).
- A dashboard that disagrees with the tree or exports about an image's status. One status calculation, used everywhere.

## Constraints and decisions made with the user

- **Versions:** a shared core library plus a UI package for Umbraco v14 and later (new Lit/web-component backoffice). v13 is dropped. Following uRestore, target the latest supported major first (uRestore pins `[17.0.0, 18.0.0)`, net10), then widen. v18/v19 specifics are to be verified against Umbraco source, not assumed.
- **Consent model:** full consent record (user decision).
- **Phasing:** compliance first, front-end gated access later (user decision).
- **Same repo layout as uRestore:** `src/<Package>`, `src/<Package>.TestSite`, `Client/` (Vite/TypeScript), `umbraco-marketplace.json`, `docs/README_nuget.md`.

## My calls (not yet agreed by the user)

- **Storage:** custom database tables (People, ConsentRecord, ImageGovernance/tagging join, audit) via Umbraco's migration framework, rather than properties on media types. Reason: works without UI Builder, supports consent history and auditing, and keeps Phase 2 access rules separable. Trade-off: media is not self-describing in the standard property editor UI.
- **Backoffice UI:** a Media-section dashboard/workspace view plus a workspace view/tab on each image, built as Umbraco backoffice extensions backed by a custom Management API controller set (as uRestore does).
- **Expiry rule:** expiry = custom expiry if set, otherwise date taken + default retention. If an image has people tagged, the earliest of that and any consent expiry/withdrawal applies.
- **Scheduled job:** Umbraco background job (hosted service), respecting load-balanced/multi-server environments.
- **Export:** CSV.

## Decisions on people and consent (from the user)

- **People are linked records.** A person record can be linked to an existing Umbraco member or user, so the package does not duplicate identity data. *My interpretation of "linked": the link is optional, because many people pictured (students, visitors, the public) are not members or users. Please check this.* Beyond the link, a person holds name and optional contact details (my call).
- **Consent is per image.** Each consent record ties one person to one image, with who gave it, when, permitted uses, optional expiry or withdrawal, and optional evidence. History is retained.
- **Erasure on request.** An editor with permission can erase a person: their details, all their consent records and any evidence files are permanently deleted. The person's tags are removed from images, so an image left with no tagged people returns to "Needs tagging". The audit log keeps an anonymised entry ("person erased", date, acting user) with no personal data. Evidence files are stored so they can be found and deleted per person.

## More calls from the builder check (mine, not yet agreed by the user)

- **First release target:** Umbraco v17 / net10 only, as uRestore does. Widening to v14-v16 is a later release, not Phase 1. Keep core logic free of version-specific APIs to make that cheap.
- **Status:** computed on read from stored facts (tags, consent records, dates, settings), never persisted. Design for thousands of images with server-side filtering and paging. The scheduled job only detects transitions (to notify), so it needs a small record of the last-notified status per image.
- **Permissions and audit:** reuse Media section access. Audit entries store the Umbraco user key, or "system" for the scheduled job. Status dots show only to users who can see the media tree.
- **Scope of media:** image media types only. Other types are ignored. Governance rows are keyed by media GUID (stable for Phase 2) and cleaned up when media is deleted.
- **Date taken:** manual entry, with optional prefill from EXIF where present. A separate "date obtained" field is the fallback when date taken is unknown. Existing images start as "Needs tagging".
- **Consent scope:** a configurable list of permitted uses (default: Web, Print, Social, Other). A consent is "active" if given, not withdrawn and not expired. Phase 1 does not compare scope against actual usage.
- **Notifications:** recipients set in Settings as email addresses or a user group. One notification per status transition, not repeated.
- **Prototype:** the Claude Design prototype is the visual and behavioural reference for the dashboard, tiles and table. The builder gets screenshots or the exported HTML; the design is not rebuilt from the URL.

## Status rules (first draft, to confirm)

Evaluated in order, first match wins:
1. **Expired**: effective expiry date is in the past.
2. **No consent**: people tagged, but no active (given, not withdrawn, not expired) consent covers them.
3. **Needs tagging**: people not recorded (neither tagged nor explicitly marked "no people").
4. **Expiring soon**: effective expiry within the warning window.
5. **Compliant**: otherwise.

Note: the prototype shows images that are both expired and without consent under a single status; the precedence above is my interpretation. Images explicitly marked "No people" skip consent checks.

## Amendments after approval

Changes made at the user's direction while building Phase 1. Where these conflict with earlier sections, these win.

- **Front-end protection moved into Phase 1.** Hiding an image in a page does not stop someone who has its URL, so Phase 1 now also: (a) provides a `<media-governance>` tag helper and an `IMediaGovernanceUsage` service so templates can leave out withheld images; (b) writes a de-duplicated warning to the Umbraco log naming the page whenever a withheld image is requested; (c) returns **404** for the file of a withheld image at `/media/...` (everyone except signed-in backoffice users), on by default, switchable with `Umbraco:Community:MediaGovernance:BlockWithheldMediaFiles`.
  - "Withheld" means status **Expired** or **No consent**. Expiring soon and Needs tagging are still usable.
  - This overrides the non-goal "front-end gated access" only for withheld files. Member groups, the email gate, a download endpoint, usage tracking and watermarking remain Phase 2.
- **Pages affected tab.** A dashboard tab listing the pages that use withheld images, built from Umbraco's tracked references (media pickers, rich text, blocks). It cannot see images hardcoded in templates; those are covered by the log warnings.
- **Governance changes save immediately.** The image Governance tab has no separate Save button. Each change (tagging, dates, "no people", consent) saves as it is made, so Umbraco's own Save is the only Save in the UI.
- **Consent "Given by" is a fixed list** (the person themselves, a parent or guardian, a legal representative), enforced by the server. A consent needs at least one permitted use, and every use must be in the configured list.
- **Settings validation.** Recipients are trimmed, de-duplicated and validated (at most 25); permitted uses are 1 to 12 entries of at most 30 characters with no commas or line breaks.
- **Custom calendar** replaces the browser's date picker, which cannot be styled.
- **Retention start date.** Date taken, then date obtained, then the media item's upload date. Nothing is written to the image; it is a fallback, so older untouched images are judged by when they were uploaded. File blocking covers every image, not only ones with governance data, so the page and the file always agree.
- **Email covers Needs tagging**, grouped by status and capped at 50 per group.
- **Several problems at once.** Status has an ordered list of issues (Expired, No consent, Needs tagging, Expiring soon); the headline status is the first. The tree shows a sign for each and the Governance tab shows each as a pill.
- **Tree colours and folder markers.** Image icons are coloured by status (red, dark amber, green); folders get a red or amber marker and icon colour when anything inside needs attention.
- **Not built:** a "!" badge on the Governance workspace tab (Umbraco's tab badges are tied to a per-view hint system and need a workspace-context extension).
- **Deferred still:** EXIF prefill of date taken; user-group notification recipients; media start-node enforcement; evidence on shared storage for scaled-out sites.

## Phase 2 (deferred, context only)

Gated front-end access per the SRM brief: member-group access rules on media folders/collections, secure download endpoint (Umbraco serves `/media` statically, so protected media must be served through a controller/middleware), email gate with GDPR considerations, usage/download logging, optional watermarking, large-media delivery and external blob storage. Phase 1's data model should keep media identifiers and audit logging reusable for this.

## Left to the builder

- Exact table schemas, indexes and migration approach.
- Management API shape and naming.
- Client UI component structure, following uRestore conventions.
- Notification mechanism (email via Umbraco email sender, or backoffice notifications).
- Test strategy and test-site seed data (the prototype's 12 images and 6 people are good seed data).
