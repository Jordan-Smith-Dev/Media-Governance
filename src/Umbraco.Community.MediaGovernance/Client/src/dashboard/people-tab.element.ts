import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import { STATUS_LABELS, type Person, type PersonImage, type PersonRequest } from "../api/index.js";
import { STATUS_COLORS, formatDate } from "./format.js";

type Editing = { id: string | null; form: PersonRequest; consentGiven: boolean; original: boolean };

const EMPTY: PersonRequest = { name: "", email: null, phone: null, linkedMemberKey: null, linkedUserKey: null, jobTitle: null };

const imageHref = (key: string) => `/umbraco/section/media/workspace/media/edit/${key}/invariant/view/governance`;

@customElement("umb-media-governance-people")
export class MediaGovernancePeopleElement extends GovernanceElement {
    @state() private _people: Person[] = [];
    @state() private _query = "";
    @state() private _editing: Editing | null = null;
    @state() private _images: PersonImage[] = [];
    @state() private _loading = true;
    @state() private _error = "";
    #debounce?: number;
    /** From ?person=<id>: that person's panel opens once the list has loaded (used by links from an image). */
    #focusId = new URLSearchParams(window.location.search).get("person") ?? "";

    override connectedCallback() {
        super.connectedCallback();
        void this.#load();
    }

    async #load() {
        this._loading = true;
        this._error = "";
        try {
            this._people = await this.api.people(this._query);
            const focus = this._people.find((p) => p.id === this.#focusId);
            if (focus) {
                this.#focusId = "";
                void this.#edit(focus);
            }
        } catch (e) {
            this._error = errorMessage(e);
        } finally {
            this._loading = false;
        }
    }

    #onQuery(e: Event) {
        this._query = (e.target as HTMLInputElement).value;
        window.clearTimeout(this.#debounce);
        this.#debounce = window.setTimeout(() => void this.#load(), 250);
    }

    async #edit(person: Person | null) {
        this._images = [];
        this._editing = person
            ? { id: person.id, form: { ...person }, consentGiven: person.consentGiven, original: person.consentGiven }
            : { id: null, form: { ...EMPTY }, consentGiven: false, original: false };
        if (person) {
            try {
                this._images = await this.api.personImages(person.id);
            } catch (e) {
                this.notify("danger", errorMessage(e));
            }
        }
    }

    #close() {
        this._editing = null;
        this._images = [];
    }

    #set(field: keyof PersonRequest, value: string) {
        if (!this._editing) return;
        this._editing = { ...this._editing, form: { ...this._editing.form, [field]: value.trim() === "" ? null : value } };
    }

    async #save() {
        if (!this._editing) return;
        const { id, form, consentGiven, original } = this._editing;
        if (!form.name?.trim()) {
            this.notify("danger", "A person needs a name.");
            return;
        }
        try {
            const saved = id ? await this.api.updatePerson(id, form) : await this.api.createPerson(form);
            // Consent is recorded on the person; only a change is sent, so the audit trail shows real changes.
            if (consentGiven !== original) await this.api.setConsent(saved.id, consentGiven);
            this.#close();
            this.notify("positive", "Person saved");
            await this.#load();
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    async #erase(person: Person) {
        try {
            await umbConfirmModal(this, {
                headline: `Erase ${person.name}?`,
                content: "This permanently deletes this person's details and consent, and removes them from all images. It cannot be undone.",
                color: "danger",
                confirmLabel: "Erase permanently",
            });
        } catch {
            return; // cancelled
        }
        try {
            await this.api.erasePerson(person.id);
            this.#close();
            this.notify("positive", "Person erased");
            await this.#load();
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    override render() {
        return html`
            <div class="toolbar">
                <uui-input label="Search people" placeholder="Search people..." .value=${this._query} @input=${this.#onQuery}></uui-input>
                <uui-button class="action" look="primary" label="Add person" @click=${() => this.#edit(null)}>Add person</uui-button>
            </div>
            ${this._error ? html`<uui-tag color="danger">${this._error}</uui-tag>` : nothing}
            ${this._loading
                ? html`<uui-loader></uui-loader>`
                : this._people.length === 0
                  ? html`<p class="empty">No people yet. Use Add person, then tag them in images from each image's Governance tab.</p>`
                  : this.#renderTable()}
            ${this.#renderPanel()}
        `;
    }

    #renderTable() {
        return html`
            <uui-table>
                <uui-table-head>
                    <uui-table-head-cell>Name</uui-table-head-cell>
                    <uui-table-head-cell>Job title</uui-table-head-cell>
                    <uui-table-head-cell>Email</uui-table-head-cell>
                    <uui-table-head-cell>Consent</uui-table-head-cell>
                    <uui-table-head-cell>Linked to</uui-table-head-cell>
                    <uui-table-head-cell>Tagged in</uui-table-head-cell>
                    <uui-table-head-cell></uui-table-head-cell>
                </uui-table-head>
                ${this._people.map(
                    (p) => html`
                        <uui-table-row>
                            <uui-table-cell><button class="link" @click=${() => this.#edit(p)}>${p.name}</button></uui-table-cell>
                            <uui-table-cell>${p.jobTitle ?? "—"}</uui-table-cell>
                            <uui-table-cell>${p.email ?? "—"}</uui-table-cell>
                            <uui-table-cell>
                                <uui-tag look="primary" color=${p.consentGiven ? "positive" : "danger"}>${p.consentGiven ? "Consent given" : "No consent"}</uui-tag>
                            </uui-table-cell>
                            <uui-table-cell>
                                ${p.linkedMemberKey ? html`<uui-tag>Member</uui-tag>` : nothing}
                                ${p.linkedUserKey ? html`<uui-tag>User</uui-tag>` : nothing}
                                ${!p.linkedMemberKey && !p.linkedUserKey ? "—" : nothing}
                            </uui-table-cell>
                            <uui-table-cell>${p.taggedInCount} ${p.taggedInCount === 1 ? "image" : "images"}</uui-table-cell>
                            <uui-table-cell class="actions">
                                <div class="action-buttons">
                                    <uui-button class="action" look="secondary" label="Edit" @click=${() => this.#edit(p)}>Edit</uui-button>
                                    <uui-button class="action" look="primary" color="danger" label="Erase" @click=${() => this.#erase(p)}>Erase</uui-button>
                                </div>
                            </uui-table-cell>
                        </uui-table-row>
                    `
                )}
            </uui-table>
        `;
    }

    /** The slide-out panel: details, consent, and the images the person is tagged in. */
    #renderPanel() {
        const e = this._editing;
        if (!e) return nothing;
        const f = e.form;
        const person = e.id ? this._people.find((p) => p.id === e.id) : undefined;
        return html`
            <div class="overlay" @click=${this.#close}></div>
            <aside class="panel" role="dialog" aria-label=${e.id ? "Edit person" : "Add person"}>
                <header>
                    <h3>${e.id ? f.name || "Edit person" : "Add person"}</h3>
                    <uui-button compact label="Close" @click=${this.#close}><uui-icon name="icon-wrong"></uui-icon></uui-button>
                </header>

                <div class="body">
                    <uui-box headline="Details">
                        <div class="form">
                            <uui-label for="name" required>Name</uui-label>
                            <uui-input id="name" label="Name" .value=${f.name} @input=${(ev: Event) => this.#set("name", (ev.target as HTMLInputElement).value)}></uui-input>
                            <uui-label for="job">Job title</uui-label>
                            <uui-input id="job" label="Job title" .value=${f.jobTitle ?? ""} @input=${(ev: Event) => this.#set("jobTitle", (ev.target as HTMLInputElement).value)}></uui-input>
                            <uui-label for="email">Email</uui-label>
                            <uui-input id="email" label="Email" type="email" .value=${f.email ?? ""} @input=${(ev: Event) => this.#set("email", (ev.target as HTMLInputElement).value)}></uui-input>
                            <uui-label for="phone">Phone</uui-label>
                            <uui-input id="phone" label="Phone" .value=${f.phone ?? ""} @input=${(ev: Event) => this.#set("phone", (ev.target as HTMLInputElement).value)}></uui-input>
                            <uui-label>Linked member (optional)</uui-label>
                            <umb-input-member max="1" .value=${f.linkedMemberKey ?? ""} @change=${(ev: Event) => this.#set("linkedMemberKey", (ev.target as HTMLInputElement).value ?? "")}></umb-input-member>
                            <uui-label>Linked user (optional)</uui-label>
                            <umb-user-input max="1" .value=${f.linkedUserKey ?? ""} @change=${(ev: Event) => this.#set("linkedUserKey", (ev.target as HTMLInputElement).value ?? "")}></umb-user-input>
                        </div>
                    </uui-box>

                    <uui-box headline="Consent">
                        <uui-checkbox
                            label="Consent given"
                            .checked=${e.consentGiven}
                            @change=${(ev: Event) => (this._editing = { ...e, consentGiven: (ev.target as HTMLInputElement).checked })}>
                            This person has given consent to appear in images
                        </uui-checkbox>
                        <p class="muted">
                            ${person?.consentUpdatedUtc
                                ? html`Last changed ${formatDate(person.consentUpdatedUtc.slice(0, 10))}. Every change is recorded with who made it.`
                                : html`Every change is recorded with who made it.`}
                            Without consent, every image this person is tagged in shows as No consent.
                        </p>
                    </uui-box>

                    ${e.id
                        ? html`
                              <uui-box headline="Tagged in ${this._images.length} ${this._images.length === 1 ? "image" : "images"}">
                                  ${this._images.length === 0
                                      ? html`<p class="muted">Not tagged in any image yet. Tag them from an image's Governance tab.</p>`
                                      : html`<ul class="images">
                                            ${this._images.map(
                                                (i) => html`<li>
                                                    <a href=${imageHref(i.mediaKey)}>${i.name}</a>
                                                    <span class="muted">${i.folder}</span>
                                                    <uui-tag color=${STATUS_COLORS[i.status]}>${STATUS_LABELS[i.status]}</uui-tag>
                                                </li>`
                                            )}
                                        </ul>`}
                              </uui-box>
                          `
                        : nothing}
                </div>

                <footer>
                    ${person ? html`<uui-button class="action" look="primary" color="danger" label="Erase" @click=${() => this.#erase(person)}>Erase</uui-button>` : html`<span></span>`}
                    <div class="buttons">
                        <uui-button class="action" look="secondary" label="Cancel" @click=${this.#close}>Cancel</uui-button>
                        <uui-button class="action" look="primary" color="positive" label="Save" @click=${this.#save}>Save</uui-button>
                    </div>
                </footer>
            </aside>
        `;
    }

    static override styles = [buttonStyles, css`
        :host { display: block; }
        .toolbar { display: flex; gap: var(--uui-size-space-4); margin-bottom: var(--uui-size-space-4); }
        .toolbar uui-input { flex: 1; }
        .actions { white-space: nowrap; }
        .action-buttons { display: flex; align-items: center; justify-content: flex-end; gap: var(--uui-size-space-2); }
        .empty, .muted { color: var(--uui-color-text-alt); }
        .muted { font-size: var(--uui-type-small-size); }
        button.link { all: unset; cursor: pointer; color: var(--uui-color-interactive); font-weight: 600; }
        button.link:hover { text-decoration: underline; }
        button.link:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }

        /* The slide-out panel */
        .overlay { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.35); z-index: 100; animation: fade 0.15s ease-out; }
        .panel { position: fixed; top: 0; right: 0; bottom: 0; width: min(560px, 100vw); z-index: 101; display: flex; flex-direction: column;
            background: var(--uui-color-surface); color: var(--uui-color-text); box-shadow: var(--uui-shadow-depth-5); animation: slide 0.2s ease-out; }
        .panel header, .panel footer { display: flex; align-items: center; justify-content: space-between; gap: var(--uui-size-space-4); padding: var(--uui-size-space-4) var(--uui-size-space-5); }
        .panel header { border-bottom: 1px solid var(--uui-color-border); }
        .panel header h3 { margin: 0; }
        .panel footer { border-top: 1px solid var(--uui-color-border); }
        .panel .body { flex: 1; overflow: auto; padding: var(--uui-size-space-5); display: flex; flex-direction: column; gap: var(--uui-size-space-5); }
        .form { display: grid; grid-template-columns: 160px 1fr; gap: var(--uui-size-space-3) var(--uui-size-space-4); align-items: center; }
        .buttons { display: flex; gap: var(--uui-size-space-3); }
        .images { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--uui-size-space-3); }
        .images li { display: flex; align-items: center; gap: var(--uui-size-space-3); }
        .images li a { font-weight: 600; color: var(--uui-color-interactive); text-decoration: none; }
        .images li a:hover { text-decoration: underline; }
        .images li uui-tag { margin-left: auto; }
        @keyframes slide { from { transform: translateX(100%); } to { transform: none; } }
        @keyframes fade { from { opacity: 0; } to { opacity: 1; } }
        @media (prefers-reduced-motion: reduce) { .panel, .overlay { animation: none; } }
    `];
}

export default MediaGovernancePeopleElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-people": MediaGovernancePeopleElement;
    }
}
