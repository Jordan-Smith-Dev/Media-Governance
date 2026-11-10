# Media Governance Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Progress (2026-11-02):** Code, unit tests (197 passing) and packaging are done. Remaining: the manual TestSite checks in Tasks 3, 5, 6 and 7 (steps left unticked), and committing.

**Goal:** Ship Phase 1 of the Media Governance Umbraco package: an image consent, tagging and expiry compliance dashboard in the Media section, with no UI Builder dependency.

**Architecture:** A pure `Core` library holds models and the status calculator (no Umbraco dependency, unit-tested). The package project holds persistence (custom tables via Umbraco migrations), services, a Management API and the Lit/TypeScript backoffice client, following uRestore's layout. Status is computed on read; a recurring job only detects transitions to notify.

**Tech Stack:** .NET 10, Umbraco 17 (`[17.0.0, 18.0.0)`), NPoco via Umbraco scopes, xUnit, Lit/TypeScript/Vite backoffice bundle.

**Spec:** `docs/superpowers/specs/2026-10-29-media-governance-design.md`

## Global Constraints

- Umbraco `[17.0.0, 18.0.0)`, `net10.0` only for Phase 1; v13 not supported; no UI Builder dependency.
- Package id and root namespace: `Umbraco.Community.MediaGovernance`; core library `Umbraco.Community.MediaGovernance.Core`; API name constant `umbracocommunitymediagovernance`.
- Layout mirrors `C:\DEV-PERSONAL\uRestore\src` (package project, TestSite, `Client/` Vite bundle output to `wwwroot/App_Plugins/UmbracoCommunityMediaGovernance`).
- Defaults: retention 3 years from date taken (fallback: date obtained); warning window 90 days; permitted uses Web, Print, Social, Other.
- Status precedence (first match wins): Expired, No consent, Needs tagging, Expiring soon, Compliant.
- Image media types only; governance keyed by media GUID; status never persisted.
- Permissions reuse Media section access (`AuthorizationPolicies.BackOfficeAccess` plus media section policy); audit stores Umbraco user key or `"system"`.
- Erasure hard-deletes person details, consent records and evidence files; audit keeps an anonymised entry.
- Commits: do not commit unless the user has authorised it; work on a feature branch.
- Verify Umbraco 17 extension and API specifics against source or docs before relying on them (use the `umbraco-upgrade:umbraco-source-verifier` agent); do not guess.

## Review Focus

- Expiry exactly today: expiry date == today is Expired (not Expiring soon).
- No date taken and no date obtained and no custom expiry: no effective expiry, so never Expired or Expiring soon.
- Image marked "No people" that also has tags: tags win; it needs consent.
- Erasing a person on an image with other people: the other tags and consents remain; the image does not drop to Needs tagging.
- Erasing a person linked to a Umbraco member/user must never delete the member/user.
- Evidence file upload: filename traversal (`..\`), oversized and disallowed file types rejected.
- Recurring job on multi-server setups: runs only on the main server; no duplicate notifications after restart.
- Dashboard with thousands of images: filtering and paging happen server-side.

---

## File Structure

```
Media-Governance/
  global.json                                         (copy uRestore's)
  src/Umbraco.Community.MediaGovernance.sln
  src/Umbraco.Community.MediaGovernance.Core/        pure logic, no Umbraco refs
    Models/  GovernanceStatus.cs, GovernanceSettings.cs, ImageGovernanceFacts.cs, ConsentFacts.cs
    StatusCalculator.cs
  src/Umbraco.Community.MediaGovernance.Tests/       xUnit, references Core (and package for service tests)
  src/Umbraco.Community.MediaGovernance/            Razor SDK package
    Composers/MediaGovernanceComposer.cs
    Persistence/  Schema/*.cs, Migrations/*.cs, Repositories/*.cs
    Services/     PersonService, ConsentService, ImageGovernanceService, OverviewService,
                  SettingsService, AuditService, EvidenceStore, ExportService, NotificationService
    Handlers/     MediaDeletedHandler.cs, MediaSavingExifHandler.cs
    Jobs/         ExpiryTransitionJob.cs
    Controllers/  (one controller per area)
    Client/       Vite project (src/dashboard, src/workspace-views, src/api, manifests)
  src/Umbraco.Community.MediaGovernance.TestSite/   Umbraco 17 site + seed data
  docs/README_nuget.md, umbraco-marketplace.json, CHANGELOG.md
```

---

### Task 1: Scaffold solution, projects and test site

**Files:** Create everything under `src/` listed above (csproj files, sln, `.gitignore`, `global.json`, `Constants.cs`); model on uRestore's csproj and TestSite.

**Interfaces:**
- Produces: `Umbraco.Community.MediaGovernance.Constants { const string ApiName = "umbracocommunitymediagovernance"; }`; empty projects that build; Tests project referencing Core.

- [x] **Step 1:** Create the three .NET projects and TestSite from uRestore's csproj/`Program.cs`/`appsettings*.json` (SQLite, unattended install, dev credentials in `appsettings.Development.json` only), package refs `[17.0.0, 18.0.0)`.
- [x] **Step 2:** Add xUnit test project (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`) referencing Core.
- [x] **Step 3:** Run `dotnet build src/Umbraco.Community.MediaGovernance.sln` and `dotnet test src/Umbraco.Community.MediaGovernance.sln`. Expected: build succeeds, 0 tests run.

### Task 2: Core models and StatusCalculator (TDD)

**Files:** Create `Core/Models/*`, `Core/StatusCalculator.cs`; Test `Tests/StatusCalculatorTests.cs`.

**Interfaces:**
- Produces:
  - `enum GovernanceStatus { Compliant, ExpiringSoon, Expired, NoConsent, NeedsTagging }`
  - `record GovernanceSettings(int RetentionYears, int WarningDays)` with defaults 3 and 90
  - `record ConsentFacts(Guid PersonId, DateOnly GivenOn, DateOnly? ExpiresOn, DateOnly? WithdrawnOn)`
  - `record ImageGovernanceFacts(Guid MediaKey, DateOnly? DateTaken, DateOnly? DateObtained, DateOnly? CustomExpiry, bool NoPeople, IReadOnlyList<Guid> TaggedPersonIds, IReadOnlyList<ConsentFacts> Consents)`
  - `static class StatusCalculator { static DateOnly? EffectiveExpiry(ImageGovernanceFacts f, GovernanceSettings s); static GovernanceStatus Calculate(ImageGovernanceFacts f, GovernanceSettings s, DateOnly today); }`

- [x] **Step 1: Write failing tests** in `StatusCalculatorTests`, one per rule, with `today = 2026-10-29`: `Expired_when_expiry_is_today` (date taken 2023-10-29 → expiry 2026-10-29 → Expired); `ExpiringSoon_when_within_90_days`; `NoConsent_when_person_tagged_without_active_consent`; `Consent_withdrawn_or_expired_is_not_active`; `NeedsTagging_when_no_people_recorded_and_not_NoPeople`; `NoPeople_flag_skips_consent_and_tagging`; `Tags_override_NoPeople_flag`; `CustomExpiry_overrides_default`; `Falls_back_to_DateObtained`; `No_dates_means_no_expiry`; `Earliest_active_consent_expiry_caps_effective_expiry` (consent expiring before retention expiry wins); `Expired_beats_NoConsent`; `Every_tagged_person_needs_their_own_active_consent`.
- [x] **Step 2:** `dotnet test --filter StatusCalculatorTests`. Expected: FAIL (types missing).
- [x] **Step 3:** Implement the types and `StatusCalculator` in Core per the precedence in Global Constraints. Expiry = `CustomExpiry ?? (DateTaken ?? DateObtained)?.AddYears(RetentionYears)`, then `min` with expiries of active consents. Expiring soon when `today < expiry <= today + WarningDays`.
- [x] **Step 4:** `dotnet test --filter StatusCalculatorTests`. Expected: all PASS.

### Task 3: Persistence schema, migration, repositories

**Files:** Create `Persistence/Schema/*` (NPoco POCOs with `[TableName]`, `[PrimaryKey]`), `Persistence/Migrations/MediaGovernanceMigrationPlan.cs` + `CreateTables.cs`, `Persistence/Repositories/*`; Modify `Composers/MediaGovernanceComposer.cs`.

**Interfaces:**
- Tables: `mgPerson(Id Guid, Name, Email?, Phone?, LinkedMemberKey Guid?, LinkedUserKey Guid?, CreatedUtc)`; `mgImage(MediaKey Guid PK, DateTaken?, DateObtained?, CustomExpiry?, NoPeople bit)`; `mgImagePerson(MediaKey, PersonId)`; `mgConsent(Id, MediaKey, PersonId, GivenOn, GivenBy text, PermittedUses text, ExpiresOn?, WithdrawnOn?, EvidencePath?, Notes?)`; `mgAudit(Id, WhenUtc, ActorKey text, Action, MediaKey?, PersonId?, Detail)`; `mgNotified(MediaKey PK, LastStatus, NotifiedUtc)`.
- Produces: repository interfaces `IPersonRepository`, `IImageRepository`, `IConsentRepository`, `IAuditRepository`, `INotifiedRepository` with async CRUD plus `IImageRepository.QueryFactsAsync(...)` returning `ImageGovernanceFacts` (joins tags and consents), and `IPersonRepository.EraseAsync(Guid personId)` removing the person, their tags, consents (returning evidence paths).

- [x] **Step 1:** Write `MediaGovernanceMigrationPlan : AsyncPackageMigrationPlan` named `"Umbraco.Community.MediaGovernance"` and `CreateTables : AsyncMigrationBase` creating each table if absent; register via `IComposer` + a notification handler on `UmbracoApplicationStartingNotification` (run the upgrader, as Umbraco's package migration docs describe; verify against v17 docs).
- [x] **Step 2:** Implement repositories with `IScopeProvider`/`IUmbracoDatabase`.
- [x] **Step 3:** Run TestSite (`dotnet run`), install, confirm the six tables exist in the SQLite DB (`sqlite3` or DB tool), and a restart does not re-run the migration. Expected: tables present, no errors in logs. *(Not re-verified: needs a TestSite run.)* *(Verified 2026-11-02: six planned tables plus `mgMediaFile`; restart is clean.)*

### Task 4: Services with unit tests (fakes)

**Files:** Create `Services/PersonService.cs`, `ConsentService.cs`, `ImageGovernanceService.cs`, `AuditService.cs`, `SettingsService.cs` (uses `IKeyValueService`), `EvidenceStore.cs`; Tests `Tests/PersonServiceTests.cs`, `ConsentServiceTests.cs` (hand-written fake repositories).

**Interfaces:**
- Produces:
  - `PersonService`: `Task<PersonDto> CreateAsync(PersonCreate, string actor)`, `UpdateAsync`, `ListAsync(string? q)`, `Task EraseAsync(Guid id, string actor)`
  - `ConsentService`: `Task<ConsentDto> AddAsync(Guid mediaKey, ConsentCreate, string actor)`, `WithdrawAsync(Guid consentId, DateOnly on, string actor)`, `ListForImageAsync(Guid mediaKey)`
  - `ImageGovernanceService`: `GetAsync(Guid mediaKey)`, `SaveAsync(Guid mediaKey, ImageGovernanceUpdate, string actor)` (dates, NoPeople, tagged person ids)
  - `SettingsService`: `Task<GovernanceSettings> GetAsync()`, `SaveAsync(GovernanceSettings)`; also notification recipients and permitted-use list
  - `EvidenceStore`: `Task<string> SaveAsync(Guid personId, Guid consentId, string fileName, Stream data)`; `DeleteForPersonAsync(Guid personId)`; rejects path traversal, files over 10 MB, extensions other than pdf/png/jpg/jpeg

- [x] **Step 1: Write failing tests:** `Erase_removes_person_tags_consents_and_evidence`; `Erase_keeps_other_peoples_tags_and_consents`; `Erase_writes_anonymised_audit_entry` (no name/email in `Detail`); `Erase_never_touches_linked_member` (fake member service call count 0); `Consent_requires_tagged_person`; `EvidenceStore_rejects_traversal_oversize_and_bad_extension`.
- [x] **Step 2:** `dotnet test`. Expected: FAIL.
- [x] **Step 3:** Implement services; every mutating method writes an audit row via `AuditService.WriteAsync(actor, action, mediaKey?, personId?, detail)`.
- [x] **Step 4:** `dotnet test`. Expected: PASS.

### Task 5: OverviewService, export, Management API

**Files:** Create `Services/OverviewService.cs`, `ExportService.cs`, `Controllers/OverviewController.cs`, `PeopleController.cs`, `ConsentController.cs`, `SettingsController.cs`, `ImageController.cs`, `Models/*` DTOs; modify `Composers` to register services and Swagger doc (copy uRestore's controller attribute pattern and its composer/swagger registration).

**Interfaces:**
- `OverviewService.QueryAsync(OverviewQuery{ GovernanceStatus? Status; string? Filter; int Skip; int Take }) → OverviewPage{ Counts per status, Items[], Total }` (counts always cover all images; filtering and paging server-side; loads only image media types).
- Routes under `umbraco/umbracocommunitymediagovernance/api/v1`: `GET overview`, `GET overview/export` (CSV), `GET/PUT image/{mediaKey}`, `POST image/{mediaKey}/consent`, `POST consent/{id}/withdraw`, `POST consent/{id}/evidence`, `GET/POST/PUT people`, `DELETE people/{id}/erase`, `GET/PUT settings`, `GET image/status-map` (mediaKey → status, for tree dots).

- [x] **Step 1: Write failing tests** for `OverviewService` using fake repository: `Counts_cover_all_images_regardless_of_filter`; `Paging_returns_requested_slice`; `Status_filter_matches_StatusCalculator`; `Text_filter_matches_name_folder_person`.
- [x] **Step 2:** Run tests, expect FAIL; implement; run tests, expect PASS.
- [x] **Step 3:** Implement controllers; actor = current backoffice user key (`IBackOfficeSecurityAccessor`). Run TestSite, open `/umbraco/swagger/umbracocommunitymediagovernance/swagger.json` and call each endpoint while logged in. Expected: 200s, 401 when logged out, 404 for unknown ids. *(Verified 2026-11-10 on https://localhost:44399 (OpenIddict needs HTTPS and `ASPNETCORE_ENVIRONMENT=Development`): all 7 GET endpoints 401 logged out, 200 with a bearer token; unknown ids 404.)*

### Task 6: Handlers and scheduled job

**Files:** Create `Handlers/MediaDeletedHandler.cs`, `Handlers/MediaSavingExifHandler.cs`, `Jobs/ExpiryTransitionJob.cs`, `Services/NotificationService.cs`; Tests `Tests/ExpiryTransitionTests.cs`.

**Interfaces:**
- `MediaDeletedHandler : INotificationAsyncHandler<MediaDeletedNotification>` deletes `mgImage`, tags, consents, evidence for the media key.
- `MediaSavingExifHandler`: on saving a new image with no date taken, prefill from EXIF DateTimeOriginal where present (use a small library such as `MetadataExtractor`; verify licence/compat).
- `ExpiryTransitionJob : RecurringHostedServiceBase`, daily, main-server only via `IServerRoleAccessor`; uses pure `TransitionDetector.Detect(IEnumerable<(Guid, GovernanceStatus current, GovernanceStatus? lastNotified)>) → IReadOnlyList<Transition>` and `NotificationService.SendAsync(Transition[])` (emails via `IEmailSender` to the recipients in settings; user-group recipients resolved via `IUserService`).

- [x] **Step 1: Write failing tests** for `TransitionDetector`: `Notifies_on_entering_ExpiringSoon_or_Expired_or_NoConsent_once`; `Does_not_repeat_for_unchanged_status`; `Notifies_again_after_status_changes_and_returns`.
- [x] **Step 2:** Run, expect FAIL; implement; run, expect PASS.
- [x] **Step 3:** Implement handlers and job; manual check in TestSite: delete a media item and confirm its rows are gone; trigger the job (debug endpoint or short interval in Development) with a SMTP drop folder; confirm one email per transition. *(Verified 2026-11-10 with an SMTP pickup folder: one digest listed the 5 images needing attention; permanently deleting a media item removed its `mgNotified` and `mgMediaFile` rows and wrote an `image.deleted` audit entry; a second job run emailed only the one new transition, not the earlier four. EXIF prefill, `MediaSavingExifHandler`, was deferred and is not implemented.)*

### Task 7: Backoffice client: dashboard, image tab, tree dots

**Files:** Create `Client/` (copy uRestore's `package.json`, `tsconfig.json`, `vite.config.ts`, adjusting names/output dir), `Client/public/umbraco-package.json`, `Client/src/bundle.manifests.ts`, `src/api/client.ts` (typed fetch wrapper to the API with the backoffice auth token), `src/dashboard/*` (Overview, People, Settings tabs), `src/workspace-views/image-governance.element.ts`, `src/tree/status-dot.*`.

**Interfaces:**
- Consumes: Task 5 routes.
- Produces: a Media-section dashboard "Media governance" with Overview (filter tiles and table per the prototype, export button), People (list, create, edit, link member, erase with confirm), Settings (retention, warning window, permitted uses, notification recipients); an "Image governance" workspace view on image media items (date taken/obtained, custom expiry, No people, tag people, consent records with add/withdraw/evidence upload); status dots in the media tree.
- Note: the prototype's top-level "Repositories" section is a mock; People lives in the dashboard People tab (deviation, mention in README).

- [x] **Step 1:** Verify against Umbraco 17 source/docs the manifest types to use: `dashboard` (Media section condition), `workspaceView` on media workspace (condition on image media types), and the supported way to decorate tree items (entity-item/tree-item extension). If no supported decoration exists, record it and fall back to a status column in the dashboard only, reporting to the user.
- [x] **Step 2:** Implement the client; `cd Client && npm install && npm run build`. Expected: bundle in `wwwroot/App_Plugins/UmbracoCommunityMediaGovernance`, `tsc` clean.
- [x] **Step 3:** Run TestSite and verify in the browser with Chrome automation: tiles and counts match the seed data; filters; add person and consent; erase person; settings persist; export downloads CSV. Capture screenshots. *(Verified 2026-11-02 in Chrome: tiles and counts, Expired filter, CSV export (header plus 15 rows), add person, erase person (audit entry anonymised), settings save and persist, image Governance tab, record and withdraw consent, tree markers. Not checked: evidence file upload.)*

### Task 8: Seed data, docs and packaging

**Files:** Create TestSite seed (migration or startup handler in TestSite only) with the prototype's 12 images and 6 people; `docs/README_nuget.md`, `umbraco-marketplace.json`, `CHANGELOG.md`, package metadata in csproj (copy uRestore's structure).

- [x] **Step 1:** Seed matching the prototype (statuses: 2 needs tagging, 2 no consent, 3 expiring soon, 2 expired out of 12, relative to dates so they stay true).
- [x] **Step 2:** `dotnet pack -c Release` on the package project. Expected: nupkg contains `App_Plugins` bundle and README. Run `dotnet test` once more. Expected: all PASS. *(Pack verified 2026-11-02: nupkg has the App_Plugins bundle, README and both DLLs; 197 tests pass.)*
- [x] **Step 3:** Update the spec if any deviations occurred (tree dots, People location) and list them in the CHANGELOG.

---

## Self-Review

- **Spec coverage:** dashboard/tiles/table/export (T5, T7); tree dots (T7, with fallback); People repo + member/user link (T3, T4, T7); per-image full consent with evidence (T3, T4, T7); settings (T4, T7); status rules (T2); scheduled evaluation and notifications (T6); erasure (T4); no UI Builder (T1 package refs); media deletion cleanup (T6); EXIF prefill (T6); audit (T3, T4).
- **Type consistency:** `GovernanceStatus`, `ImageGovernanceFacts`, `ConsentFacts`, `GovernanceSettings` are defined in T2 and consumed unchanged in T3 to T6.
- **Known risk:** Task 3 migration bootstrap and Task 7 tree decoration depend on Umbraco 17 APIs that must be verified, not assumed.
