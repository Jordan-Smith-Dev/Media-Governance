import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import { formatDate } from "./format.js";

const splitList = (text: string) =>
    text
        .split(/[,\n]/)
        .map((s) => s.trim())
        .filter(Boolean);

const EMAIL = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/;
const MAX_RECIPIENTS = 25;
const MAX_USES = 12;
const MAX_USE_LENGTH = 30;

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

@customElement("umb-media-governance-settings")
export class MediaGovernanceSettingsElement extends GovernanceElement {
    @state() private _years = "3";
    @state() private _days = "90";
    @state() private _uses = "";
    @state() private _emails = "";
    @state() private _blockFiles = true;
    @state() private _hidePages = true;
    @state() private _loaded = false;
    @state() private _error = "";
    @state() private _saving = false;
    @state() private _baseline = "";

    override connectedCallback() {
        super.connectedCallback();
        void this.#load();
    }

    async #load() {
        try {
            const s = await this.api.settings();
            this._years = String(s.retentionYears);
            this._days = String(s.warningDays);
            this._uses = s.permittedUses.join(", ");
            this._emails = s.notificationEmails.join("\n");
            this._blockFiles = s.blockWithheldFiles ?? true;
            this._hidePages = s.hideWithheldInPages ?? true;
            this._baseline = this.#snapshot();
            this._loaded = true;
        } catch (e) {
            this._error = errorMessage(e);
        }
    }

    #snapshot() {
        return JSON.stringify([this._years, this._days, splitList(this._uses), splitList(this._emails), this._blockFiles, this._hidePages]);
    }

    get #dirty() {
        return this.#snapshot() !== this._baseline;
    }

    get #yearsError() {
        const n = Number(this._years);
        return !Number.isInteger(n) || n < 1 || n > 100 ? "Enter a whole number of years from 1 to 100." : "";
    }

    get #daysError() {
        const n = Number(this._days);
        return !Number.isInteger(n) || n < 0 || n > 3650 ? "Enter a whole number of days from 0 to 3650." : "";
    }

    get #usesError() {
        const uses = this.#uniqueUses;
        if (uses.length === 0) return "Add at least one permitted use, otherwise consent cannot be recorded.";
        if (uses.length > MAX_USES) return `Use at most ${MAX_USES} permitted uses.`;
        const tooLong = uses.find((u) => u.length > MAX_USE_LENGTH);
        return tooLong ? `"${tooLong.slice(0, 20)}…" is too long: keep each use under ${MAX_USE_LENGTH} characters.` : "";
    }

    get #uniqueUses() {
        return this.#unique(splitList(this._uses));
    }

    get #uniqueEmails() {
        return this.#unique(splitList(this._emails));
    }

    #unique(list: string[]) {
        const seen = new Set<string>();
        return list.filter((v) => !seen.has(v.toLowerCase()) && seen.add(v.toLowerCase()));
    }

    get #emailError() {
        const emails = this.#uniqueEmails;
        const bad = emails.filter((e) => e.length > 254 || !EMAIL.test(e));
        if (bad.length > 0) {
            const shown = bad.slice(0, 3).join(", ");
            return `Not a valid email address: ${shown}${bad.length > 3 ? ` and ${bad.length - 3} more` : ""}.`;
        }
        return emails.length > MAX_RECIPIENTS ? `Use at most ${MAX_RECIPIENTS} recipients.` : "";
    }

    get #duplicateEmails() {
        return splitList(this._emails).length - this.#uniqueEmails.length;
    }

    get #valid() {
        return !this.#yearsError && !this.#daysError && !this.#usesError && !this.#emailError;
    }

    async #save() {
        if (!this.#valid) return;
        this._saving = true;
        try {
            await this.api.saveSettings({
                retentionYears: Number(this._years),
                warningDays: Number(this._days),
                permittedUses: splitList(this._uses),
                notificationEmails: splitList(this._emails),
                blockWithheldFiles: this._blockFiles,
                hideWithheldInPages: this._hidePages,
            });
            await this.#load();
            this.notify("positive", "Settings saved");
        } catch (e) {
            this.notify("danger", errorMessage(e));
        } finally {
            this._saving = false;
        }
    }

    /** What the retention rules mean for an image taken today. */
    #example() {
        if (this.#yearsError || this.#daysError) return nothing;
        const expiry = new Date();
        expiry.setFullYear(expiry.getFullYear() + Number(this._years));
        const flagged = new Date(expiry);
        flagged.setDate(flagged.getDate() - Number(this._days));
        return html`
            <div class="example">
                An image taken today can be used until <strong>${formatDate(iso(expiry))}</strong>${Number(this._days) > 0
                    ? html`, and is flagged as <em>Expiring soon</em> from <strong>${formatDate(iso(flagged))}</strong>`
                    : nothing}.
            </div>
        `;
    }

    #number(field: "_years" | "_days", e: Event) {
        this[field] = (e.target as HTMLInputElement).value;
    }

    override render() {
        if (this._error) return html`<uui-tag color="danger">${this._error}</uui-tag>`;
        if (!this._loaded) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="page">
                <uui-box headline="Retention rules">
                    <umb-property-layout label="Default retention" description="How long an image can be used, counted from the date taken (or obtained).">
                        <div slot="editor" class="field">
                            <div class="with-unit">
                                <uui-input label="Default retention in years" type="number" min="1" max="100" .value=${this._years} @input=${(e: Event) => this.#number("_years", e)}></uui-input>
                                <span>years</span>
                            </div>
                            ${this.#yearsError ? html`<span class="error">${this.#yearsError}</span>` : nothing}
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label="Warning window" description="Flag images as Expiring soon this many days before they expire.">
                        <div slot="editor" class="field">
                            <div class="with-unit">
                                <uui-input label="Warning window in days" type="number" min="0" max="3650" .value=${this._days} @input=${(e: Event) => this.#number("_days", e)}></uui-input>
                                <span>days</span>
                            </div>
                            ${this.#daysError ? html`<span class="error">${this.#daysError}</span>` : nothing}
                        </div>
                    </umb-property-layout>
                    ${this.#example()}
                </uui-box>

                <uui-box headline="Consent">
                    <umb-property-layout label="Permitted uses" description="The uses an editor can tick when recording consent. Separate them with commas.">
                        <div slot="editor" class="field">
                            <uui-input label="Permitted uses" .value=${this._uses} @input=${(e: Event) => (this._uses = (e.target as HTMLInputElement).value)}></uui-input>
                            <div class="chips">${this.#uniqueUses.map((u) => html`<uui-tag look="secondary">${u}</uui-tag>`)}</div>
                            ${this.#usesError ? html`<span class="error">${this.#usesError}</span>` : nothing}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="Front end (your public website)">
                    <umb-property-layout label="Hide withheld images in pages" description="Applies to markup wrapped in the <media-governance> tag helper. When on, an image that has Expired or has no consent is left out of the page. When off, it still shows (and is still written to the log, so you can find the page).">
                        <div slot="editor" class="field">
                            <uui-toggle label="Hide withheld images in pages" .checked=${this._hidePages} @change=${(e: Event) => (this._hidePages = (e.target as HTMLInputElement).checked)}>
                                ${this._hidePages ? "Hidden" : "Still shown"}
                            </uui-toggle>
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label="Block withheld image files" description="When on, anyone requesting the file of an Expired image, or an image of someone without consent, gets a 404, even if they have the address. Signed-in editors can still see it. When off, only pages are affected.">
                        <div slot="editor" class="field">
                            <uui-toggle label="Block withheld image files" .checked=${this._blockFiles} @change=${(e: Event) => (this._blockFiles = (e.target as HTMLInputElement).checked)}>
                                ${this._blockFiles ? "Blocked" : "Still served"}
                            </uui-toggle>
                            ${!this._blockFiles ? html`<span class="warn">Withheld images can still be reached by their address, and may still appear in search engines.</span>` : nothing}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="Notifications">
                    <umb-property-layout label="Recipients" description="Email addresses that get a daily digest when images need attention. One per line. Leave empty to send nothing.">
                        <div slot="editor" class="field">
                            <uui-textarea label="Notification recipients" .value=${this._emails} @input=${(e: Event) => (this._emails = (e.target as HTMLTextAreaElement).value)}></uui-textarea>
                            ${this.#emailError ? html`<span class="error">${this.#emailError}</span>` : nothing}
                            ${!this.#emailError && this.#duplicateEmails > 0 ? html`<span class="muted">${this.#duplicateEmails} duplicate ${this.#duplicateEmails === 1 ? "address" : "addresses"} will be merged when you save.</span>` : nothing}
                            ${!this.#emailError && this.#uniqueEmails.length > 0 ? html`<span class="muted">${this.#uniqueEmails.length} ${this.#uniqueEmails.length === 1 ? "recipient" : "recipients"}</span>` : nothing}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <div class="actions">
                    <span class="unsaved">${this.#dirty ? "You have unsaved changes" : ""}</span>
                    <uui-button class="action" look="primary" color="positive" label="Save settings" ?disabled=${this._saving || !this.#dirty || !this.#valid} @click=${this.#save}>
                        Save settings
                    </uui-button>
                </div>
            </div>
        `;
    }

    static override styles = [buttonStyles, css`
        :host { display: block; }
        .page { display: flex; flex-direction: column; gap: var(--uui-size-layout-1); width: 100%; }
        uui-box { --uui-box-default-padding: var(--uui-size-space-6); }
        umb-property-layout { padding: var(--uui-size-space-4) 0; }
        umb-property-layout:first-of-type { padding-top: 0; }
        umb-property-layout:last-of-type { padding-bottom: 0; }
        .field { display: flex; flex-direction: column; gap: var(--uui-size-space-3); }
        .with-unit { display: flex; align-items: center; gap: var(--uui-size-space-3); }
        .with-unit uui-input { width: 120px; }
        .with-unit span { color: var(--uui-color-text-alt); }
        .chips { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
        .warn { color: var(--uui-color-warning-standalone, #b45309); font-size: var(--uui-type-small-size); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
        .error { color: var(--uui-color-danger-standalone); font-size: var(--uui-type-small-size); }
        .example { margin-top: var(--uui-size-space-5); padding: var(--uui-size-space-4) var(--uui-size-space-5); background: var(--uui-color-surface-alt); border-left: 3px solid var(--uui-color-interactive); border-radius: var(--uui-border-radius); }
        uui-textarea { width: 100%; max-width: 520px; }
        uui-input { max-width: 520px; width: 100%; }
        .with-unit uui-input { width: 120px; }
        .actions { display: flex; justify-content: flex-end; align-items: center; gap: var(--uui-size-space-5); }
        .unsaved { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
    `];
}

export default MediaGovernanceSettingsElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-settings": MediaGovernanceSettingsElement;
    }
}
