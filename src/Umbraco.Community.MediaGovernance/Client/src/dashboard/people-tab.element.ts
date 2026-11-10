import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import type { Person, PersonRequest } from "../api/index.js";

type Editing = { id: string | null; form: PersonRequest };

const EMPTY: PersonRequest = { name: "", email: null, phone: null, linkedMemberKey: null, linkedUserKey: null, jobTitle: null };

@customElement("umb-media-governance-people")
export class MediaGovernancePeopleElement extends GovernanceElement {
    /** When set, that person is opened for editing once the list has loaded. */
    @property() focusPersonId = "";
    @state() private _people: Person[] = [];
    @state() private _query = "";
    @state() private _editing: Editing | null = null;
    @state() private _loading = true;
    @state() private _error = "";
    #debounce?: number;

    override connectedCallback() {
        super.connectedCallback();
        void this.#load();
    }

    async #load() {
        this._loading = true;
        this._error = "";
        try {
            this._people = await this.api.people(this._query);
            const focus = this._people.find((p) => p.id === this.focusPersonId);
            if (focus) {
                this.focusPersonId = "";
                this.#edit(focus);
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

    #edit(person: Person | null) {
        this._editing = person
            ? { id: person.id, form: { ...person } }
            : { id: null, form: { ...EMPTY } };
    }

    #set(field: keyof PersonRequest, value: string) {
        if (!this._editing) return;
        this._editing = { ...this._editing, form: { ...this._editing.form, [field]: value.trim() === "" ? null : value } };
    }

    async #save() {
        if (!this._editing) return;
        const { id, form } = this._editing;
        if (!form.name?.trim()) {
            this.notify("danger", "A person needs a name.");
            return;
        }
        try {
            if (id) await this.api.updatePerson(id, form);
            else await this.api.createPerson(form);
            this._editing = null;
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
                content:
                    "This permanently deletes this person's details, every consent record and any evidence files, and removes them from all images. It cannot be undone.",
                color: "danger",
                confirmLabel: "Erase permanently",
            });
        } catch {
            return; // cancelled
        }
        try {
            await this.api.erasePerson(person.id);
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
                <uui-button look="primary" label="Add person" @click=${() => this.#edit(null)}>Add person</uui-button>
            </div>
            ${this._error ? html`<uui-tag color="danger">${this._error}</uui-tag>` : nothing} ${this.#renderForm()}
            ${this._loading
                ? html`<uui-loader></uui-loader>`
                : this._people.length === 0
                  ? html`<p class="empty">No people yet. People can be tagged in images once added.</p>`
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
                    <uui-table-head-cell>Phone</uui-table-head-cell>
                    <uui-table-head-cell>Linked to</uui-table-head-cell>
                    <uui-table-head-cell>Tagged in</uui-table-head-cell>
                    <uui-table-head-cell></uui-table-head-cell>
                </uui-table-head>
                ${this._people.map(
                    (p) => html`
                        <uui-table-row>
                            <uui-table-cell>${p.name}</uui-table-cell>
                            <uui-table-cell>${p.jobTitle ?? "—"}</uui-table-cell>
                            <uui-table-cell>${p.email ?? "—"}</uui-table-cell>
                            <uui-table-cell>${p.phone ?? "—"}</uui-table-cell>
                            <uui-table-cell>
                                ${p.linkedMemberKey ? html`<uui-tag>Member</uui-tag>` : nothing}
                                ${p.linkedUserKey ? html`<uui-tag>User</uui-tag>` : nothing}
                                ${!p.linkedMemberKey && !p.linkedUserKey ? "—" : nothing}
                            </uui-table-cell>
                            <uui-table-cell>${p.taggedInCount} ${p.taggedInCount === 1 ? "image" : "images"}</uui-table-cell>
                            <uui-table-cell class="actions">
<div class="action-buttons">
                                <uui-button look="secondary" label="Edit" @click=${() => this.#edit(p)}>Edit</uui-button>
                                <uui-button look="primary" color="danger" label="Erase" @click=${() => this.#erase(p)}>Erase</uui-button>
</div>
</uui-table-cell>
                        </uui-table-row>
                    `
                )}
            </uui-table>
        `;
    }

    #renderForm() {
        if (!this._editing) return nothing;
        const f = this._editing.form;
        return html`
            <uui-box headline=${this._editing.id ? "Edit person" : "Add person"}>
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
                <div class="buttons">
                    <uui-button look="secondary" label="Cancel" @click=${() => (this._editing = null)}>Cancel</uui-button>
                    <uui-button look="primary" color="positive" label="Save" @click=${this.#save}>Save</uui-button>
                </div>
            </uui-box>
        `;
    }

    static override styles = css`
        :host { display: block; }
        .toolbar { display: flex; gap: var(--uui-size-space-4); margin-bottom: var(--uui-size-space-4); }
        .toolbar uui-input { flex: 1; }
        .form { display: grid; grid-template-columns: 200px 1fr; gap: var(--uui-size-space-3) var(--uui-size-space-4); align-items: center; }
        .buttons { display: flex; justify-content: flex-end; gap: var(--uui-size-space-3); margin-top: var(--uui-size-space-4); }
        uui-box { margin-bottom: var(--uui-size-space-5); }
        .actions { white-space: nowrap; }
        .action-buttons { display: flex; align-items: center; justify-content: flex-end; gap: var(--uui-size-space-2); }
        .empty { color: var(--uui-color-text-alt); }
    `;
}

export default MediaGovernancePeopleElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-people": MediaGovernancePeopleElement;
    }
}
