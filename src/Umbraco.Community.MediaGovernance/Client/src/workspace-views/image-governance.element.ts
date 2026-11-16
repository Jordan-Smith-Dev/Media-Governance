import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UMB_MEDIA_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/media";
import "../components/date-input.element.js";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import type { ImageGovernance, Person } from "../api/index.js";
import { daysUntil, formatDate, relativeTime } from "../dashboard/format.js";

const MAX_SUGGESTIONS = 8;

/** Where a person's page lives, so a tagged person links straight to their record. */
const personHref = (id: string) => `/umbraco/section/media/workspace/media-governance/view/people?person=${id}`;

@customElement("umb-media-governance-image-view")
export class MediaGovernanceImageViewElement extends GovernanceElement {
    @state() private _mediaKey = "";
    @state() private _data?: ImageGovernance;
    @state() private _people: Person[] = [];
    @state() private _error = "";
    @state() private _saveState: "idle" | "saving" | "saved" = "idle";
    @state() private _personSearch = "";
    #pending: Promise<unknown> = Promise.resolve();
    #savedTimer?: number;

    constructor() {
        super();
        this.consumeContext(UMB_MEDIA_WORKSPACE_CONTEXT, (ctx) => {
            this.observe(ctx?.unique, (unique) => {
                if (unique && unique !== this._mediaKey) {
                    this._mediaKey = unique;
                    void this.#load();
                }
            });
        });
    }

    async #load() {
        this._error = "";
        try {
            const [data, people] = await Promise.all([this.api.image(this._mediaKey), this.api.people()]);
            this._data = data;
            this._people = people;
        } catch (e) {
            this._error = errorMessage(e);
        }
    }

    /** Applies a change and saves it immediately, so the only Save button in the UI is Umbraco's own. */
    #patch(change: Partial<ImageGovernance>) {
        if (!this._data) return;
        this._data = { ...this._data, ...change };
        void this.#persist();
    }

    #date(field: "dateTaken" | "dateObtained", e: Event) {
        const value = (e as CustomEvent<string>).detail;
        this.#patch({ [field]: value || null });
    }

    /**
     * The Expiry field starts at the calculated date. Choosing a different date stores it as the image's own expiry;
     * choosing the calculated date (or clearing the field) goes back to following the retention period.
     */
    #expiry(e: Event) {
        const d = this._data;
        if (!d) return;
        const value = (e as CustomEvent<string>).detail;
        this.#patch({ customExpiry: !value || value === d.defaultExpiry ? null : value });
    }

    #addPerson(id: string) {
        if (!this._data || this._data.taggedPersonIds.includes(id)) return;
        this.#patch({ taggedPersonIds: [...this._data.taggedPersonIds, id], noPeople: false });
        this._personSearch = "";
    }

    #removePerson(id: string) {
        if (!this._data) return;
        this.#patch({ taggedPersonIds: this._data.taggedPersonIds.filter((p) => p !== id) });
    }

    /** Saves the current details. Saves are queued so they reach the server in order. */
    #persist(): Promise<unknown> {
        this._saveState = "saving";
        this.#pending = this.#pending.then(async () => {
            const d = this._data;
            if (!d) return;
            try {
                const saved = await this.api.saveImage(this._mediaKey, {
                    dateTaken: d.dateTaken,
                    dateObtained: d.dateObtained,
                    customExpiry: d.customExpiry,
                    noPeople: d.noPeople,
                    taggedPersonIds: d.taggedPersonIds,
                });
                // Keep the status and expiry fresh; keep what the user has typed since this save started.
                const current = this._data;
                this._data = current
                    ? { ...saved, dateTaken: current.dateTaken, dateObtained: current.dateObtained, customExpiry: current.customExpiry, noPeople: current.noPeople, taggedPersonIds: current.taggedPersonIds }
                    : saved;
                this._saveState = "saved";
                window.clearTimeout(this.#savedTimer);
                this.#savedTimer = window.setTimeout(() => (this._saveState = "idle"), 2500);
            } catch (e) {
                this.notify("danger", `Not saved: ${errorMessage(e)}`);
                this._saveState = "idle";
                await this.#load(); // show what the server actually holds
            }
        });
        return this.#pending;
    }

    #person(id: string): Person | undefined {
        return this._people.find((p) => p.id === id);
    }

    override render() {
        if (this._error) return html`<div class="wrap"><uui-tag color="danger">${this._error}</uui-tag></div>`;
        const d = this._data;
        if (!d) return html`<div class="wrap"><uui-loader></uui-loader></div>`;

        return html`
            <div class="wrap">
                <uui-box headline="Image details">
                    <umb-property-layout label="Date taken" description=${this.#dateTakenHint(d)}>
                        <umb-media-governance-date-input slot="editor" label="Date taken" .value=${d.dateTaken ?? ""} @change=${(e: Event) => this.#date("dateTaken", e)}></umb-media-governance-date-input>
                        ${!d.dateTaken && d.exifDateTaken
                            ? html`<uui-button slot="editor" look="secondary" compact label="Use the date from the photo" @click=${() => this.#patch({ dateTaken: d.exifDateTaken })}>
                                  Use ${formatDate(d.exifDateTaken)} from the photo's camera data
                              </uui-button>`
                            : nothing}
                    </umb-property-layout>
                    <umb-property-layout label="Date obtained" description="Used when the date taken is not known.">
                        <umb-media-governance-date-input slot="editor" label="Date obtained" .value=${d.dateObtained ?? ""} @change=${(e: Event) => this.#date("dateObtained", e)}></umb-media-governance-date-input>
                    </umb-property-layout>
                    <umb-property-layout label="Expiry" description=${this.#expiryHint(d)}>
                        <div slot="editor" class="expiry">
                            <umb-media-governance-date-input label="Expiry" .value=${d.effectiveExpiry ?? ""} @change=${this.#expiry}></umb-media-governance-date-input>
                            ${this.#renderExpiredIndicator(d)}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="People pictured">
                    <umb-property-layout label="Contains no people" description="Tick when nobody is pictured, so the image does not need tagging.">
                        <uui-toggle slot="editor" label="Contains no people" .checked=${d.noPeople} ?disabled=${d.taggedPersonIds.length > 0} @change=${(e: Event) => this.#patch({ noPeople: (e.target as HTMLInputElement).checked })}></uui-toggle>
                    </umb-property-layout>
                    <umb-property-layout label="Tagged people" description="Green: the person has given consent. Red: they have not. Consent is recorded on the person, in Governance, People.">
                        <div slot="editor" class="people">${this.#renderPeoplePicker(d)}</div>
                    </umb-property-layout>
                </uui-box>

                <p class="autosave-note">
                    <span class="save-state">${this._saveState === "saving" ? "Saving..." : this._saveState === "saved" ? "Saved" : ""}</span>
                    Governance details save as you change them. The Save button below saves the media item itself.
                </p>
            </div>
        `;
    }

    #dateTakenHint(d: ImageGovernance): string {
        if (d.dateTaken) return "Retention is counted from this date.";
        if (d.dateObtained) return "Not set: the date obtained is used instead.";
        if (d.dateUploaded) return `Not set: the upload date (${formatDate(d.dateUploaded)}) is used instead.`;
        return "Retention is counted from this date.";
    }

    #expiryHint(d: ImageGovernance): string {
        if (!d.effectiveExpiry) return "No expiry yet. Add a date taken or obtained, or choose a date here.";
        if (d.customExpiry) return `Your own date. Clear it to go back to the calculated expiry (${formatDate(d.defaultExpiry)}).`;
        return "Calculated from the retention period. Choose another date to set your own.";
    }

    /** A red tag once the expiry has passed, with how long ago. */
    #renderExpiredIndicator(d: ImageGovernance) {
        if (!d.effectiveExpiry) return nothing;
        const days = daysUntil(d.effectiveExpiry);
        if (days > 0) return html`<span class="muted">${relativeTime(days)}</span>`;
        return html`<uui-tag color="danger" look="primary" title="The expiry date has passed. Do not use this image.">Expired ${days === 0 ? "today" : relativeTime(days)}</uui-tag>`;
    }

    #renderPeoplePicker(d: ImageGovernance) {
        const available = this._people.filter((p) => !d.taggedPersonIds.includes(p.id));
        const term = this._personSearch.trim().toLowerCase();
        const matches = available.filter((p) => !term || p.name.toLowerCase().includes(term));

        return html`
            <div class="tags">
                ${d.taggedPersonIds.length === 0 ? html`<span class="muted">No one tagged yet.</span>` : nothing}
                ${d.taggedPersonIds.map((id) => {
                    const p = this.#person(id);
                    const given = p?.consentGiven ?? false;
                    return html`
                        <uui-tag class="person" color=${given ? "positive" : "danger"} look="primary" title=${given ? "Has given consent" : "Has not given consent"}>
                            <span class="person-row">
                                <a class="person-link" href=${personHref(id)}>${p?.name ?? "Unknown person"}</a>
                                <span class="consent-label">${given ? "Consent given" : "No consent"}</span>
                                <uui-button class="remove" compact label="Remove ${p?.name ?? "person"}" @click=${() => this.#removePerson(id)}>
                                    <uui-icon name="icon-wrong"></uui-icon>
                                </uui-button>
                            </span>
                        </uui-tag>
                    `;
                })}
            </div>

            ${available.length === 0
                ? html`<span class="muted">${this._people.length === 0 ? "No people exist yet. Add them in Governance, People." : "Everyone is already tagged."}</span>`
                : html`
                      <uui-input label="Search people to tag" placeholder="Search people to tag..." .value=${this._personSearch} @input=${(e: Event) => (this._personSearch = (e.target as HTMLInputElement).value)}></uui-input>
                      <div class="suggestions">
                          ${matches.slice(0, MAX_SUGGESTIONS).map(
                              (p) => html`<uui-button look="outline" compact label="Tag ${p.name}" @click=${() => this.#addPerson(p.id)}>
                                  <uui-icon name="icon-add"></uui-icon> ${p.name}
                              </uui-button>`
                          )}
                          ${matches.length === 0 ? html`<span class="muted">No one matches.</span>` : nothing}
                          ${matches.length > MAX_SUGGESTIONS ? html`<span class="muted">+${matches.length - MAX_SUGGESTIONS} more, keep typing to narrow the list.</span>` : nothing}
                      </div>
                  `}
        `;
    }

    static override styles = css`
        :host { display: flex; flex-direction: column; min-height: 100%; }
        .wrap { flex: 1; padding: var(--uui-size-layout-1); display: flex; flex-direction: column; gap: var(--uui-size-layout-1); }
        uui-box { --uui-box-default-padding: var(--uui-size-space-6) var(--uui-size-space-6); }
        umb-property-layout { padding: var(--uui-size-space-4) 0; }
        umb-property-layout:first-of-type { padding-top: 0; }
        umb-property-layout:last-of-type { padding-bottom: 0; }
        .expiry { display: flex; align-items: center; gap: var(--uui-size-space-4); flex-wrap: wrap; }
        .people { display: flex; flex-direction: column; gap: var(--uui-size-space-4); }
        .tags, .suggestions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
        .autosave-note { margin: 0; color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); text-align: right; }
        .save-state { margin-right: var(--uui-size-space-3); }
        uui-input { max-width: 420px; width: 100%; }
        .person { --uui-tag-padding: 0 var(--uui-size-space-3); min-height: 36px; }
        .person[color="positive"] { --on-tag: var(--uui-color-positive-contrast); }
        .person[color="danger"] { --on-tag: var(--uui-color-danger-contrast); }
        .person-row { display: inline-flex; align-items: center; gap: var(--uui-size-space-3); min-height: 36px; color: var(--on-tag); }
        .person-link { color: inherit; font-weight: 600; text-decoration: none; }
        .person-link:hover { text-decoration: underline; }
        .consent-label { font-weight: 400; }
        /* The remove icon takes the tag's own contrast colour (white on green and red). */
        .remove { --uui-button-contrast: var(--on-tag); --uui-button-contrast-hover: var(--on-tag); color: var(--on-tag); }
        .remove uui-icon { color: var(--on-tag); }
    `;
}

export default MediaGovernanceImageViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-image-view": MediaGovernanceImageViewElement;
    }
}
