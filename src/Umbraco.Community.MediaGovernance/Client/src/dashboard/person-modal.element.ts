import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement, umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { errorMessage } from "../api/governance-element.js";
import { GovernanceApi, STATUS_LABELS, type PersonImage, type PersonRequest } from "../api/index.js";
import { STATUS_COLORS, formatDate } from "./format.js";
import type { PersonModalData, PersonModalValue } from "./person-modal.token.js";

const IMAGE_PAGE_SIZE = 25;

const EMPTY: PersonRequest = { name: "", email: null, phone: null, linkedMemberKey: null, linkedUserKey: null, jobTitle: null };

const imageHref = (key: string) => `/umbraco/section/media/workspace/media/edit/${key}/invariant/view/governance`;

/** Add or edit a person: details, consent, and the images they are tagged in. Opens as Umbraco's sidebar modal. */
@customElement("umb-media-governance-person-modal")
export class MediaGovernancePersonModalElement extends UmbModalBaseElement<PersonModalData, PersonModalValue> {
    #api = new GovernanceApi();
    #notifications?: UmbNotificationContext;

    @state() private _form: PersonRequest = { ...EMPTY };
    @state() private _consent = false;
    @state() private _images: PersonImage[] = [];
    @state() private _imageTotal = 0;
    #originalConsent = false;
    #changed = false;

    constructor() {
        super();
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this.#notifications = ctx));
    }

    override connectedCallback() {
        super.connectedCallback();
        const person = this.data?.person;
        if (person) {
            this._form = { ...person };
            this._consent = this.#originalConsent = person.consentGiven;
            void this.#loadImages(person.id);
        }
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

    async #save() {
        if (!this._form.name?.trim()) {
            this.#notify("danger", "A person needs a name.");
            return;
        }
        try {
            const id = this.data?.person?.id;
            const saved = id ? await this.#api.updatePerson(id, this._form) : await this.#api.createPerson(this._form);
            // Consent is recorded on the person; only a change is sent, so the audit trail shows real changes.
            if (this._consent !== this.#originalConsent) await this.#api.setConsent(saved.id, this._consent);
            this.#changed = true;
            this.#notify("positive", "Person saved");
            this.#close();
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    async #erase() {
        const person = this.data?.person;
        if (!person) return;
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
            await this.#api.erasePerson(person.id);
            this.#changed = true;
            this.#notify("positive", "Person deleted");
            this.#close();
        } catch (e) {
            this.#notify("danger", errorMessage(e));
        }
    }

    override render() {
        const person = this.data?.person ?? null;
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
                </uui-box>

                <uui-box headline="Consent">
                    <uui-checkbox label="Consent given" .checked=${this._consent} @change=${(e: Event) => (this._consent = (e.target as HTMLInputElement).checked)}>
                        This person has given consent to appear in images
                    </uui-checkbox>
                    <p class="muted">
                        ${person?.consentUpdatedUtc ? html`Last changed ${formatDate(person.consentUpdatedUtc.slice(0, 10))}. ` : nothing}Every change is recorded with who made it.
                        Without consent, every image this person is tagged in shows as No consent.
                    </p>
                </uui-box>

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
                    ${person ? html`<uui-button class="action erase" look="primary" color="danger" label="Delete" @click=${this.#erase}>Delete</uui-button>` : nothing}
                    <uui-button class="action" look="secondary" label="Cancel" @click=${this._rejectModal}>Cancel</uui-button>
                    <uui-button class="action" look="primary" color="positive" label="Save" @click=${this.#save}>Save</uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static override styles = [buttonStyles, css`
        uui-box { margin-bottom: var(--uui-size-space-5); }
        .form { display: grid; grid-template-columns: 160px 1fr; gap: var(--uui-size-space-3) var(--uui-size-space-4); align-items: center; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
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
