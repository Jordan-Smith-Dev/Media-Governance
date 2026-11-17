import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UMB_MEDIA_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/media";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import type { ImageGovernance, Person, Settings, TaggedPerson } from "../api/index.js";
import { daysUntil, formatDate, relativeTime } from "../dashboard/format.js";

const MAX_SUGGESTIONS = 8;

/** Where a person's page lives, so a tagged person links straight to their record. */
const personHref = (id: string) => `/umbraco/section/media/workspace/media-governance/view/people?person=${id}`;

@customElement("umb-media-governance-image-view")
export class MediaGovernanceImageViewElement extends GovernanceElement {
    @state() private _mediaKey = "";
    @state() private _data?: ImageGovernance;
    @state() private _suggestions: Person[] = [];
    @state() private _suggestionTotal = 0;
    /** Who is tagged: from the server, plus anyone just added (so a chip shows before the save returns). */
    #known = new Map<string, TaggedPerson>();
    #searchTimer?: number;
    @state() private _error = "";
    @state() private _settings?: Settings;
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
            const [data, settings] = await Promise.all([this.api.image(this._mediaKey), this._settings ? Promise.resolve(this._settings) : this.api.settings().catch(() => undefined)]);
            this._settings = settings;
            this.#remember(data);
            this._data = data;
            await this.#search();
        } catch (e) {
            this._error = errorMessage(e);
        }
    }

    #remember(data: ImageGovernance) {
        for (const p of data.people) this.#known.set(p.personId, p);
    }

    /** Looks people up as the editor types, a few at a time: there can be thousands, so they are never all loaded. */
    async #search() {
        try {
            const page = await this.api.people(this._personSearch.trim(), 0, MAX_SUGGESTIONS + 20);
            this._suggestions = page.items;
            this._suggestionTotal = page.total;
        } catch (e) {
            this._error = errorMessage(e);
        }
    }

    #onSearch(e: Event) {
        this._personSearch = (e.target as HTMLInputElement).value;
        window.clearTimeout(this.#searchTimer);
        this.#searchTimer = window.setTimeout(() => void this.#search(), 250);
    }

    /** Applies a change and saves it immediately, so the only Save button in the UI is Umbraco's own. */
    #patch(change: Partial<ImageGovernance>) {
        if (!this._data) return;
        this._data = { ...this._data, ...change };
        void this.#persist();
    }

    #date(field: "dateTaken" | "dateObtained", e: Event) {
        const value = (e.target as HTMLInputElement).value;
        this.#patch({ [field]: value || null });
    }

    /**
     * The Expiry field starts at the calculated date. Choosing a different date stores it as the image's own expiry;
     * choosing the calculated date (or clearing the field) goes back to following the retention period.
     */
    #expiry(e: Event) {
        const d = this._data;
        if (!d) return;
        const value = (e.target as HTMLInputElement).value;
        this.#patch({ customExpiry: !value || value === d.defaultExpiry ? null : value });
    }

    #addPerson(person: Person) {
        if (!this._data || this._data.taggedPersonIds.includes(person.id)) return;
        this.#known.set(person.id, { personId: person.id, name: person.name, consentGiven: person.consentInForce, consentExpired: person.consentExpired });
        this.#patch({ taggedPersonIds: [...this._data.taggedPersonIds, person.id], noPeople: false });
        this._personSearch = "";
        void this.#search();
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
                this.#remember(saved);
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

    override render() {
        if (this._error) return html`<div class="wrap"><uui-tag color="danger">${this._error}</uui-tag></div>`;
        const d = this._data;
        if (!d) return html`<div class="wrap"><uui-loader></uui-loader></div>`;

        return html`
            <div class="wrap">
                <uui-box headline="Image details">
                    <umb-property-layout label="Date taken" description=${this.#dateTakenHint(d)}>
                        <umb-input-date slot="editor" type="date" label="Date taken" .value=${d.dateTaken ?? ""} @change=${(e: Event) => this.#date("dateTaken", e)}></umb-input-date>
                        ${!d.dateTaken && d.exifDateTaken
                            ? html`<uui-button slot="editor" look="secondary" compact label="Use the date from the photo" @click=${() => this.#patch({ dateTaken: d.exifDateTaken })}>
                                  Use ${formatDate(d.exifDateTaken)} from the photo's camera data
                              </uui-button>`
                            : nothing}
                    </umb-property-layout>
                    <umb-property-layout label="Date obtained" description="Used when the date taken is not known.">
                        <umb-input-date slot="editor" type="date" label="Date obtained" .value=${d.dateObtained ?? ""} @change=${(e: Event) => this.#date("dateObtained", e)}></umb-input-date>
                    </umb-property-layout>
                    <umb-property-layout label="Expiry" description=${this.#expiryHint(d)}>
                        <div slot="editor" class="expiry">
                            <umb-input-date type="date" label="Expiry" .value=${d.effectiveExpiry ?? ""} @change=${this.#expiry}></umb-input-date>
                            ${this.#renderExpiredIndicator(d)}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="People pictured">
                    <umb-property-layout label="Contains no people" description="Tick when nobody is pictured, so the image does not need tagging.">
                        <uui-toggle slot="editor" label="Contains no people" .checked=${d.noPeople} ?disabled=${d.taggedPersonIds.length > 0} @change=${(e: Event) => this.#patch({ noPeople: (e.target as HTMLInputElement).checked })}></uui-toggle>
                    </umb-property-layout>
                    ${this.#renderCleared(d)}
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

    /** Which uses (Web, Print...) everyone pictured has agreed to. Only shown when permitted uses are configured. */
    #renderCleared(d: ImageGovernance) {
        const uses = this._settings?.permittedUses ?? [];
        if (uses.length === 0 || d.taggedPersonIds.length === 0) return nothing;
        return html`
            <umb-property-layout label="Cleared for" description="The uses everyone pictured has agreed to. A use is only cleared when every tagged person has agreed to it.">
                <div slot="editor" class="cleared">
                    ${uses.map((u) => {
                        const ok = d.clearedUses.some((c) => c.toLowerCase() === u.toLowerCase());
                        return html`<uui-tag look="primary" color=${ok ? "positive" : "danger"} title=${ok ? `Cleared for ${u}: everyone tagged has agreed to it` : `Not cleared for ${u}: someone tagged has not agreed to it`}>${ok ? "✓" : "✕"} ${u}</uui-tag>`;
                    })}
                </div>
            </umb-property-layout>
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
        if (days <= 0) return html`<uui-tag color="danger" look="primary" title="The expiry date has passed. Do not use this image.">Expired ${days === 0 ? "today" : relativeTime(days)}</uui-tag>`;
        // Same pill as Expired: amber when it is inside the warning window, green otherwise.
        return d.issues.includes("ExpiringSoon")
            ? html`<uui-tag color="warning" look="primary" title="This image is inside the warning window before it expires.">${relativeTime(days)}</uui-tag>`
            : html`<uui-tag color="positive" look="primary" title="Still within its retention period.">${relativeTime(days)}</uui-tag>`;
    }

    #renderPeoplePicker(d: ImageGovernance) {
        const matches = this._suggestions.filter((p) => !d.taggedPersonIds.includes(p.id));
        const hidden = Math.max(0, this._suggestionTotal - d.taggedPersonIds.length - Math.min(matches.length, MAX_SUGGESTIONS));

        return html`
            <div class="tags">
                ${d.taggedPersonIds.length === 0 ? html`<span class="muted">No one tagged yet.</span>` : nothing}
                ${d.taggedPersonIds.map((id) => {
                    const p = this.#known.get(id);
                    const given = p?.consentGiven ?? false;
                    return html`
                        <uui-tag class="person" color=${given ? "positive" : "danger"} look="primary" title=${given ? "Has given consent" : p?.consentExpired ? "Consent was given, but its end date has passed" : "Has not given consent"}>
                            <span class="person-row">
                                <a class="person-link" href=${personHref(id)}>${p?.name ?? "Unknown person"}</a>
                                <span class="consent-label">${given ? "Consent given" : p?.consentExpired ? "Consent expired" : "No consent"}</span>
                                <uui-button class="remove" compact label="Remove ${p?.name ?? "person"}" @click=${() => this.#removePerson(id)}>
                                    <uui-icon name="icon-wrong"></uui-icon>
                                </uui-button>
                            </span>
                        </uui-tag>
                    `;
                })}
            </div>

            <uui-input label="Search people to tag" placeholder="Search people to tag..." .value=${this._personSearch} @input=${this.#onSearch}></uui-input>
            <div class="suggestions">
                ${matches.slice(0, MAX_SUGGESTIONS).map(
                    (p) => html`<uui-button look="outline" compact label="Tag ${p.name}" @click=${() => this.#addPerson(p)}>
                        <uui-icon name="icon-add"></uui-icon> ${p.name}
                    </uui-button>`
                )}
                ${matches.length === 0
                    ? html`<span class="muted">${this._personSearch.trim() ? "No one matches." : this._suggestionTotal === 0 ? "No people exist yet. Add them in Governance, People." : "Everyone is already tagged."}</span>`
                    : nothing}
                ${hidden > 0 ? html`<span class="muted">+${hidden} more, keep typing to narrow the list.</span>` : nothing}
            </div>
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
        .cleared { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); }
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
