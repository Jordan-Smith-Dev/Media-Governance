import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement, umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { errorMessage } from "../api/governance-element.js";
import { GovernanceApi, STATUS_LABELS, type ConsentChange, type EvidenceEvent, type Person, type PersonImage, type PersonRequest, type Settings } from "../api/index.js";
import { GIVEN_BY_OPTIONS, STATUS_COLORS, formatBytes, formatDate, todayIso } from "./format.js";
import type { PersonModalData, PersonModalValue } from "./person-modal.token.js";

const IMAGE_PAGE_SIZE = 25;

const EMPTY: PersonRequest = { name: "", email: null, phone: null, linkedMemberKey: null, linkedUserKey: null, jobTitle: null };

const imageHref = (key: string) => `/umbraco/section/media/workspace/media/edit/${key}/invariant/view/governance`;

const EVIDENCE_ACTIONS: Record<string, string> = {
    "consent.evidence": "Uploaded",
    "consent.evidence.viewed": "Viewed",
    "consent.evidence.removed": "Removed",
};

/** What was recorded for a consent, so Save only sends a change when something differs. */
type ConsentForm = { given: boolean; givenOn: string; givenBy: string; expiresOn: string; uses: string[] };

/** Add or edit a person: details, consent (with whatever optional detail is switched on), evidence, and tagged images. Opens as Umbraco's sidebar modal. */
@customElement("umb-media-governance-person-modal")
export class MediaGovernancePersonModalElement extends UmbModalBaseElement<PersonModalData, PersonModalValue> {
    #api = new GovernanceApi();
    #notifications?: UmbNotificationContext;

    @state() private _person: Person | null = null;
    @state() private _settings?: Settings;
    @state() private _form: PersonRequest = { ...EMPTY };
    @state() private _consent: ConsentForm = { given: false, givenOn: "", givenBy: "Self", expiresOn: "", uses: [] };
    @state() private _images: PersonImage[] = [];
    @state() private _imageTotal = 0;
    @state() private _history: EvidenceEvent[] | null = null;
    @state() private _busy = false;
    #original: ConsentForm = { given: false, givenOn: "", givenBy: "Self", expiresOn: "", uses: [] };
    #changed = false;

    constructor() {
        super();
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this.#notifications = ctx));
    }

    override connectedCallback() {
        super.connectedCallback();
        void this.#settings();
        const person = this.data?.person;
        if (person) {
            this.#apply(person);
            void this.#loadImages(person.id);
        }
    }

    async #settings() {
        try {
            this._settings = await this.#api.settings();
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    /** Shows the person as the server has them. */
    #apply(person: Person) {
        this._person = person;
        this._form = { ...person };
        this._consent = this.#original = {
            given: person.consentGiven,
            givenOn: person.consentGivenOn ?? "",
            givenBy: person.consentGivenBy ?? "Self",
            expiresOn: person.consentExpiresOn ?? "",
            uses: person.consentPermittedUses ?? [],
        };
    }

    /** The images are fetched a page at a time: a person can be tagged in hundreds. */
    async #loadImages(personId: string, append = false) {
        try {
            const page = await this.#api.personImages(personId, append ? this._images.length : 0, IMAGE_PAGE_SIZE);
            this._images = append ? [...this._images, ...page.items] : page.items;
            this._imageTotal = page.total;
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    #notify(color: "positive" | "danger", message: string) {
        this.#notifications?.peek(color, { data: { message } });
    }

    #close() {
        this.value = { changed: this.#changed };
        this._submitModal();
    }

    #set(field: keyof PersonRequest, value: string) {
        this._form = { ...this._form, [field]: value.trim() === "" ? null : value };
    }

    #setConsent(change: Partial<ConsentForm>) {
        this._consent = { ...this._consent, ...change };
    }

    #toggleUse(use: string, on: boolean) {
        const uses = new Set(this._consent.uses);
        if (on) uses.add(use);
        else uses.delete(use);
        this.#setConsent({ uses: [...uses] });
    }

    /** True when the consent differs from what was loaded, so there is something to record. */
    get #consentChanged() {
        const a = this._consent;
        const b = this.#original;
        if (a.given !== b.given) return true;
        if (!a.given) return false;
        const s = this._settings;
        return (
            (!!s?.consentDetails && (a.givenOn !== b.givenOn || a.givenBy !== b.givenBy)) ||
            (!!s?.consentExpiry && a.expiresOn !== b.expiresOn) ||
            (this.#usesOn && [...a.uses].sort().join() !== [...b.uses].sort().join())
        );
    }

    get #usesOn() {
        return (this._settings?.permittedUses.length ?? 0) > 0;
    }

    async #save() {
        if (!this._form.name?.trim()) {
            this.#notify("danger", "A person needs a name.");
            return;
        }
        const c = this._consent;
        if (c.given && this.#usesOn && c.uses.length === 0) {
            this.#notify("danger", "Choose at least one permitted use.");
            return;
        }
        try {
            const id = this._person?.id;
            const saved = id ? await this.#api.updatePerson(id, this._form) : await this.#api.createPerson(this._form);
            // Consent is recorded on the person; only a change is sent, so the audit trail shows real changes.
            if (this.#consentChanged) {
                const change: ConsentChange = {
                    given: c.given,
                    givenOn: this._settings?.consentDetails ? c.givenOn || todayIso() : null,
                    givenBy: this._settings?.consentDetails ? c.givenBy : null,
                    expiresOn: this._settings?.consentExpiry ? c.expiresOn || null : null,
                    permittedUses: this.#usesOn ? c.uses : null,
                };
                await this.#api.setConsent(saved.id, change);
            }
            this.#changed = true;
            this.#notify("positive", "Person saved");
            this.#close();
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    async #delete() {
        const person = this._person;
        if (!person) return;
        try {
            await umbConfirmModal(this, {
                headline: `Delete ${person.name}?`,
                content: "This permanently deletes this person's details, consent and any evidence, and removes them from all images. It cannot be undone.",
                color: "danger",
                confirmLabel: "Delete",
            });
        } catch {
            return; // cancelled
        }
        try {
            await this.#api.erasePerson(person.id);
            this.#changed = true;
            this.#notify("positive", "Person deleted");
            this.#close();
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    // ---------------------------------------------------------------- evidence and export

    #browseEvidence() {
        (this.shadowRoot?.querySelector("#evidence-dropzone") as (HTMLElement & { browse?: () => void }) | null)?.browse?.();
    }

    async #uploadEvidence(e: Event) {
        const dropzone = e.target as HTMLElement & { browse?: () => void };
        const file = (e as CustomEvent<{ files?: File[] }>).detail?.files?.[0];
        const person = this._person;
        if (!file || !person) return;
        this._busy = true;
        try {
            this.#apply(await this.#api.uploadEvidence(person.id, file));
            this.#changed = true;
            this._history = null;
            this.#notify("positive", "Evidence uploaded");
        } catch (err) {
            this.#notify("danger", errorMessage(err));
        } finally {
            this._busy = false;
            void dropzone;
        }
    }

    /** Opens the evidence in a new tab. The window is opened first, while the click still counts, then pointed at the file. */
    async #viewEvidence() {
        const person = this._person;
        if (!person) return;
        const win = window.open("about:blank", "_blank");
        try {
            const blob = await this.#api.evidence(person.id);
            const url = URL.createObjectURL(blob);
            if (win) win.location.href = url;
            else this.#download(url);
            window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
            this._history = null;
        } catch (e) {
            win?.close();
            this.#notify("danger", `Evidence could not be opened: ${errorMessage(e)}`);
        }
    }

    #download(url: string) {
        const a = document.createElement("a");
        a.href = url;
        a.download = `consent-evidence.${(this._person?.evidenceType ?? "pdf").toLowerCase()}`;
        a.click();
    }

    async #removeEvidence() {
        const person = this._person;
        if (!person) return;
        try {
            await umbConfirmModal(this, {
                headline: "Remove the evidence?",
                content: "The file is deleted. The consent itself stays as it is.",
                color: "danger",
                confirmLabel: "Remove evidence",
            });
        } catch {
            return;
        }
        try {
            this.#apply(await this.#api.removeEvidence(person.id));
            this.#changed = true;
            this._history = null;
            this.#notify("positive", "Evidence removed");
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    async #toggleHistory() {
        const person = this._person;
        if (!person) return;
        if (this._history) {
            this._history = null;
            return;
        }
        try {
            this._history = await this.#api.evidenceHistory(person.id);
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    async #exportData() {
        const person = this._person;
        if (!person) return;
        try {
            const slug = person.name.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "person";
            await this.#api.exportPerson(person.id, `person-data-${slug}.zip`);
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    // ---------------------------------------------------------------- render

    override render() {
        const person = this._person;
        const f = this._form;
        return html`
            <umb-body-layout headline=${person ? person.name : "Add person"}>
                <uui-box headline="Details">
                    <div class="form">
                        <uui-label for="name" required>Name</uui-label>
                        <uui-input id="name" label="Name" .value=${f.name} @input=${(e: Event) => this.#set("name", (e.target as HTMLInputElement).value)}></uui-input>
                        <uui-label for="job">Job title</uui-label>
                        <uui-input id="job" label="Job title" .value=${f.jobTitle ?? ""} @input=${(e: Event) => this.#set("jobTitle", (e.target as HTMLInputElement).value)}></uui-input>
                        <uui-label for="email">Email</uui-label>
                        <uui-input id="email" label="Email" type="email" .value=${f.email ?? ""} @input=${(e: Event) => this.#set("email", (e.target as HTMLInputElement).value)}></uui-input>
                        <uui-label for="phone">Phone</uui-label>
                        <uui-input id="phone" label="Phone" .value=${f.phone ?? ""} @input=${(e: Event) => this.#set("phone", (e.target as HTMLInputElement).value)}></uui-input>
                        <uui-label>Linked member (optional)</uui-label>
                        <umb-input-member max="1" .value=${f.linkedMemberKey ?? ""} @change=${(e: Event) => this.#set("linkedMemberKey", (e.target as HTMLInputElement).value ?? "")}></umb-input-member>
                        <uui-label>Linked user (optional)</uui-label>
                        <umb-user-input max="1" .value=${f.linkedUserKey ?? ""} @change=${(e: Event) => this.#set("linkedUserKey", (e.target as HTMLInputElement).value ?? "")}></umb-user-input>
                    </div>
                    ${person
                        ? html`<div class="export">
                              <uui-button look="secondary" compact label="Download their data" title="A zip with everything held about this person, for a subject access request" @click=${this.#exportData}>
                                  <uui-icon name="icon-download-alt"></uui-icon> Download their data
                              </uui-button>
                          </div>`
                        : nothing}
                </uui-box>

                ${this.#renderConsent(person)} ${this.#renderEvidence(person)}

                ${person
                    ? html`
                          <uui-box headline="Tagged in ${this._imageTotal} ${this._imageTotal === 1 ? "image" : "images"}">
                              ${this._imageTotal === 0
                                  ? html`<p class="muted">Not tagged in any image yet. Tag them from an image's Governance tab.</p>`
                                  : html`<ul class="images">
                                            ${this._images.map(
                                                (i) => html`<li>
                                                    <a href=${imageHref(i.mediaKey)}>${i.name}</a>
                                                    <span class="muted">${i.folder}</span>
                                                    <uui-tag class="pill" color=${STATUS_COLORS[i.status]}>${STATUS_LABELS[i.status]}</uui-tag>
                                                </li>`
                                            )}
                                        </ul>
                                        ${this._images.length < this._imageTotal
                                            ? html`<div class="more">
                                                  <uui-button look="secondary" compact label="Show more images" @click=${() => this.#loadImages(person.id, true)}>Show more</uui-button>
                                                  <span class="muted">Showing ${this._images.length} of ${this._imageTotal}</span>
                                              </div>`
                                            : nothing}`}
                          </uui-box>
                      `
                    : nothing}

                <div slot="actions">
                    ${person ? html`<uui-button class="action erase" look="primary" color="danger" label="Delete" @click=${this.#delete}>Delete</uui-button>` : nothing}
                    <uui-button class="action" look="secondary" label="Cancel" @click=${this._rejectModal}>Cancel</uui-button>
                    <uui-button class="action" look="primary" color="positive" label="Save" @click=${this.#save}>Save</uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    #renderConsent(person: Person | null) {
        const s = this._settings;
        const c = this._consent;
        const showDetail = c.given && !!s;
        return html`
            <uui-box headline="Consent">
                <uui-checkbox label="Consent given" .checked=${c.given} @change=${(e: Event) => this.#setConsent({ given: (e.target as HTMLInputElement).checked })}>
                    This person has given consent to appear in images
                </uui-checkbox>

                ${showDetail && s!.consentDetails
                    ? html`<div class="form detail">
                          <uui-label for="given-on">Given on</uui-label>
                          <umb-input-date id="given-on" type="date" label="Given on" .value=${c.givenOn || todayIso()} max=${todayIso()} @change=${(e: Event) => this.#setConsent({ givenOn: (e.target as HTMLInputElement).value })}></umb-input-date>
                          <uui-label for="given-by">Given by</uui-label>
                          <uui-select
                              id="given-by"
                              label="Given by"
                              .options=${GIVEN_BY_OPTIONS.map((o) => ({ name: o.label, value: o.value, selected: o.value === c.givenBy }))}
                              @change=${(e: Event) => this.#setConsent({ givenBy: (e.target as HTMLSelectElement).value })}></uui-select>
                      </div>`
                    : nothing}
                ${showDetail && s!.consentExpiry
                    ? html`<div class="form detail">
                              <uui-label for="expires-on">Expires on</uui-label>
                              <umb-input-date id="expires-on" type="date" label="Expires on" .value=${c.expiresOn} @change=${(e: Event) => this.#setConsent({ expiresOn: (e.target as HTMLInputElement).value })}></umb-input-date>
                          </div>
                          <p class="muted">Leave empty if it does not expire. Once the date passes, this person has no consent in force and every image they are in becomes Expired.</p>`
                    : nothing}
                ${showDetail && this.#usesOn
                    ? html`<div class="uses">
                          <uui-label>Permitted uses</uui-label>
                          <div class="use-list">
                              ${s!.permittedUses.map(
                                  (u) => html`<uui-checkbox label=${u} .checked=${c.uses.some((x) => x.toLowerCase() === u.toLowerCase())} @change=${(e: Event) => this.#toggleUse(u, (e.target as HTMLInputElement).checked)}></uui-checkbox>`
                              )}
                          </div>
                      </div>`
                    : nothing}

                <p class="muted">
                    ${person?.consentUpdatedUtc ? html`Last changed ${formatDate(person.consentUpdatedUtc.slice(0, 10))}. ` : nothing}Every change is recorded with who made it.
                    Without consent in force, every image this person is tagged in shows as No consent.
                </p>
            </uui-box>
        `;
    }

    #renderEvidence(person: Person | null) {
        const s = this._settings;
        if (!s?.consentEvidence) return nothing;
        return html`
            <uui-box headline="Evidence">
                ${person
                    ? html`<uui-button slot="header-actions" look="default" compact label="History" @click=${this.#toggleHistory}>
                          <uui-icon name="icon-history"></uui-icon>${this._history ? "Hide history" : "History"}
                      </uui-button>`
                    : nothing}
                ${!person
                    ? html`<p class="muted">Save the person first, then add a signed form or email here.</p>`
                    : html`
                          ${person.hasEvidence
                              ? html`<uui-ref-list>
                                    <uui-ref-node
                                        class="evidence-line"
                                        name="${person.evidenceType}${person.evidenceSizeBytes != null ? ` · ${formatBytes(person.evidenceSizeBytes)}` : ""}"
                                        detail="Consent evidence"
                                        standalone>
                                        <uui-icon slot="icon" name="icon-document"></uui-icon>
                                        <uui-action-bar slot="actions">
                                            <uui-button label="View evidence" title="View" @click=${this.#viewEvidence}><uui-icon name="icon-out"></uui-icon></uui-button>
                                            <uui-button label="Remove evidence" title="Remove" @click=${this.#removeEvidence}><uui-icon name="icon-trash"></uui-icon></uui-button>
                                        </uui-action-bar>
                                    </uui-ref-node>
                                </uui-ref-list>`
                              : nothing}
                          <uui-file-dropzone
                              id="evidence-dropzone"
                              label=${person.hasEvidence ? "Drop a file here to replace the evidence" : "Drop a signed form or email here"}
                              accept=".pdf,.png,.jpg,.jpeg"
                              ?disabled=${this._busy}
                              @change=${this.#uploadEvidence}>
                              <uui-button label="Choose a file" look="outline" ?disabled=${this._busy} @click=${this.#browseEvidence}>Choose a file</uui-button>
                          </uui-file-dropzone>
                          <p class="muted">PDF, PNG or JPG, up to 10 MB.</p>
                          ${this._history ? this.#renderHistory(this._history) : nothing}
                          <p class="muted">${s.evidenceLocation ? `Kept in: ${s.evidenceLocation}. ` : ""}Only people with access to Governance can open it, and every view is recorded.</p>
                      `}
            </uui-box>
        `;
    }

    #renderHistory(events: EvidenceEvent[]) {
        if (events.length === 0) return html`<p class="muted">Nothing has happened to the evidence yet.</p>`;
        return html`
            <uui-table class="history" aria-label="Evidence history">
                <uui-table-head>
                    <uui-table-head-cell>What</uui-table-head-cell>
                    <uui-table-head-cell>Who</uui-table-head-cell>
                    <uui-table-head-cell>When</uui-table-head-cell>
                </uui-table-head>
                ${events.map(
                    (h) => html`<uui-table-row>
                        <uui-table-cell>${EVIDENCE_ACTIONS[h.action] ?? h.action}</uui-table-cell>
                        <uui-table-cell>${h.actor}</uui-table-cell>
                        <uui-table-cell>${new Date(h.whenUtc + "Z").toLocaleString()}</uui-table-cell>
                    </uui-table-row>`
                )}
            </uui-table>
        `;
    }

    static override styles = [buttonStyles, css`
        uui-box { margin-bottom: var(--uui-size-space-5); }
        .form { display: grid; grid-template-columns: 160px 1fr; gap: var(--uui-size-space-3) var(--uui-size-space-4); align-items: center; }
        .form.detail { margin-top: var(--uui-size-space-4); }
        .export { margin-top: var(--uui-size-space-4); }
        .uses { margin-top: var(--uui-size-space-4); display: flex; flex-direction: column; gap: var(--uui-size-space-3); }
        .use-list { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-5); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
        uui-file-dropzone { margin-top: var(--uui-size-space-4); }
        .history { margin-top: var(--uui-size-space-4); }
        .images { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--uui-size-space-3); }
        .images li { display: flex; align-items: center; gap: var(--uui-size-space-3); }
        .images li a { font-weight: 600; color: var(--uui-color-interactive); text-decoration: none; }
        .images li a:hover { text-decoration: underline; }
        .images li uui-tag { margin-left: auto; }
        .pill { width: 7rem; justify-content: center; text-align: center; }
        .more { display: flex; align-items: center; gap: var(--uui-size-space-4); margin-top: var(--uui-size-space-4); }
        .erase { margin-right: auto; }
    `];
}

export default MediaGovernancePersonModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-person-modal": MediaGovernancePersonModalElement;
    }
}
