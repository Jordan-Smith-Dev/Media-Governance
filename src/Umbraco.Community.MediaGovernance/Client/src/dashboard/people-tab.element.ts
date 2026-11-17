import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { umbConfirmModal, umbOpenModal } from "@umbraco-cms/backoffice/modal";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import type { Person } from "../api/index.js";
import { MEDIA_GOVERNANCE_PERSON_MODAL } from "./person-modal.token.js";

/** People fetched per request; more load on demand, like the overview. */
const PAGE_SIZE = 50;

@customElement("umb-media-governance-people")
export class MediaGovernancePeopleElement extends GovernanceElement {
    @state() private _people: Person[] = [];
    @state() private _total = 0;
    @state() private _query = "";
    @state() private _loading = true;
    @state() private _error = "";
    #debounce?: number;
    /** From ?person=<id>: that person opens once the list has loaded (used by links from an image). */
    #focusId = new URLSearchParams(window.location.search).get("person") ?? "";

    override connectedCallback() {
        super.connectedCallback();
        void this.#load();
    }

    async #load(append = false) {
        this._loading = true;
        this._error = "";
        try {
            const page = await this.api.people(this._query, append ? this._people.length : 0, PAGE_SIZE);
            this._people = append ? [...this._people, ...page.items] : page.items;
            this._total = page.total;
            const focus = this._people.find((p) => p.id === this.#focusId);
            if (focus) {
                this.#focusId = "";
                void this.#open(focus);
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

    /** Add (null) or edit a person in Umbraco's sidebar modal, then reload if anything changed. */
    async #open(person: Person | null) {
        try {
            const result = await umbOpenModal(this, MEDIA_GOVERNANCE_PERSON_MODAL, { data: { person } });
            if (result?.changed) await this.#load();
        } catch {
            // closed without saving
        }
    }

    async #erase(person: Person) {
        try {
            await umbConfirmModal(this, {
                headline: `Delete ${person.name}?`,
                content: "This permanently deletes this person's details and consent, and removes them from all images. It cannot be undone.",
                color: "danger",
                confirmLabel: "Delete",
            });
        } catch {
            return; // cancelled
        }
        try {
            await this.api.erasePerson(person.id);
            this.notify("positive", "Person deleted");
            await this.#load();
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    override render() {
        return html`
            <div class="toolbar">
                <uui-input label="Search people" placeholder="Search people..." .value=${this._query} @input=${this.#onQuery}></uui-input>
                <uui-button class="action" look="primary" label="Add person" @click=${() => this.#open(null)}>Add person</uui-button>
            </div>
            ${this._error ? html`<uui-tag color="danger">${this._error}</uui-tag>` : nothing}
            ${this._loading
                ? html`<uui-loader></uui-loader>`
                : this._people.length === 0
                  ? html`<p class="empty">No people yet. Use Add person, then tag them in images from each image's Governance tab.</p>`
                  : this.#renderTable()}
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
                            <uui-table-cell><uui-button class="name" look="default" compact label=${p.name} @click=${() => this.#open(p)}>${p.name}</uui-button></uui-table-cell>
                            <uui-table-cell>${p.jobTitle ?? "—"}</uui-table-cell>
                            <uui-table-cell>${p.email ?? "—"}</uui-table-cell>
                            <uui-table-cell>
                                <uui-tag class="pill" look="primary" color=${p.consentGiven ? "positive" : "danger"}>${p.consentGiven ? "Consent given" : "No consent"}</uui-tag>
                            </uui-table-cell>
                            <uui-table-cell>
                                ${p.linkedMemberKey ? html`<uui-tag>Member</uui-tag>` : nothing}
                                ${p.linkedUserKey ? html`<uui-tag>User</uui-tag>` : nothing}
                                ${!p.linkedMemberKey && !p.linkedUserKey ? "—" : nothing}
                            </uui-table-cell>
                            <uui-table-cell>${p.taggedInCount} ${p.taggedInCount === 1 ? "image" : "images"}</uui-table-cell>
                            <uui-table-cell class="actions">
                                <div class="action-buttons">
                                    <uui-button class="action" look="secondary" label="Edit" @click=${() => this.#open(p)}>Edit</uui-button>
                                    <uui-button class="action" look="primary" color="danger" label="Delete" @click=${() => this.#erase(p)}>Delete</uui-button>
                                </div>
                            </uui-table-cell>
                        </uui-table-row>
                    `
                )}
            </uui-table>
            <div class="footer">
                ${this._people.length < this._total
                    ? html`<uui-button class="action" label="Load more" look="secondary" ?disabled=${this._loading} @click=${() => this.#load(true)}>Load more</uui-button>`
                    : nothing}
                <span class="muted">Showing ${this._people.length} of ${this._total} ${this._total === 1 ? "person" : "people"}</span>
            </div>
        `;
    }

    static override styles = [buttonStyles, css`
        :host { display: block; padding: var(--uui-size-layout-1); }
        .toolbar { display: flex; gap: var(--uui-size-space-4); margin-bottom: var(--uui-size-space-4); }
        .toolbar uui-input { flex: 1; }
        .actions { white-space: nowrap; }
        .action-buttons { display: flex; align-items: center; justify-content: flex-end; gap: var(--uui-size-space-2); }
        .name { --uui-button-padding-left-factor: 0; --uui-button-font-weight: 600; }
        .pill { width: 8rem; justify-content: center; text-align: center; }
        .empty, .muted { color: var(--uui-color-text-alt); }
        .footer { display: flex; align-items: center; gap: var(--uui-size-space-4); margin-top: var(--uui-size-space-4); }
    `];
}

export default MediaGovernancePeopleElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-people": MediaGovernancePeopleElement;
    }
}
