import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import type { Settings, UserGroup } from "../api/index.js";
import { formatDate } from "./format.js";

const splitList = (text: string) =>
    text
        .split(/[,\n]/)
        .map((s) => s.trim())
        .filter(Boolean);

const EMAIL = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/;
const MAX_RECIPIENTS = 25;

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

/**
 * Everything starts in appsettings (section Umbraco:Community:MediaGovernance). Here the retention period, the warning
 * window and the notification recipients can be changed; everything else is shown read-only.
 */
@customElement("umb-media-governance-settings")
export class MediaGovernanceSettingsElement extends GovernanceElement {
    @state() private _settings?: Settings;
    @state() private _years = "3";
    @state() private _days = "90";
    @state() private _emails = "";
    @state() private _groups: UserGroup[] = [];
    @state() private _groupIds: string[] = [];
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
            this._settings = s;
            this._years = String(s.retentionYears);
            this._days = String(s.warningDays);
            this._emails = s.notificationEmails.join("\n");
            this._groupIds = s.notificationUserGroupIds ?? [];
            this._groups = await this.api.userGroups().catch(() => []);
            this._baseline = this.#snapshot();
        } catch (e) {
            this._error = errorMessage(e);
        }
    }

    #snapshot() {
        return JSON.stringify([this._years, this._days, splitList(this._emails), [...this._groupIds].sort()]);
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

    get #uniqueEmails() {
        const seen = new Set<string>();
        return splitList(this._emails).filter((v) => !seen.has(v.toLowerCase()) && seen.add(v.toLowerCase()));
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

    get #valid() {
        return !this.#yearsError && !this.#daysError && !this.#emailError;
    }

    /** True when the retention period differs from the saved one, so existing images are about to be re-judged. */
    get #retentionChanged() {
        return !!this._settings && !this.#yearsError && Number(this._years) !== this._settings.retentionYears;
    }

    async #save() {
        if (!this.#valid) return;
        this._saving = true;
        try {
            await this.api.saveSettings({
                retentionYears: Number(this._years),
                warningDays: Number(this._days),
                notificationEmails: splitList(this._emails),
                notificationUserGroupIds: this._groupIds,
            });
            await this.#load();
            this.notify("positive", "Settings saved");
        } catch (e) {
            this.notify("danger", errorMessage(e));
        } finally {
            this._saving = false;
        }
    }

    #toggleGroup(id: string, on: boolean) {
        this._groupIds = on ? [...new Set([...this._groupIds, id])] : this._groupIds.filter((g) => g !== id);
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

    override render() {
        if (this._error) return html`<uui-tag color="danger">${this._error}</uui-tag>`;
        const s = this._settings;
        if (!s) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="page">
                <uui-box headline="Retention rules">
                    <umb-property-layout label="Retention period" description="How long an image can be used, counted from the date taken (or obtained, or uploaded).">
                        <div slot="editor" class="field">
                            <div class="with-unit">
                                <uui-input label="Retention in years" type="number" min="1" max="100" .value=${this._years} @input=${(e: Event) => (this._years = (e.target as HTMLInputElement).value)}></uui-input>
                                <span>years</span>
                            </div>
                            <span class="muted">Starting value from appsettings: ${s.defaultRetentionYears} ${s.defaultRetentionYears === 1 ? "year" : "years"}.</span>
                            ${this.#yearsError ? html`<span class="error">${this.#yearsError}</span>` : nothing}
                            ${this.#retentionChanged
                                ? html`<div class="callout" role="alert">
                                      <strong>This affects existing images, not just new ones.</strong>
                                      Expiry dates are worked out from the retention period every time, so saving a new value changes the expiry of every image
                                      that does not have its own expiry date, straight away. Shortening it can make images expire, and expired images are
                                      withheld from your website.
                                  </div>`
                                : nothing}
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label="Warning window" description="Flag images as Expiring soon this many days before they expire.">
                        <div slot="editor" class="field">
                            <div class="with-unit">
                                <uui-input label="Warning window in days" type="number" min="0" max="3650" .value=${this._days} @input=${(e: Event) => (this._days = (e.target as HTMLInputElement).value)}></uui-input>
                                <span>days</span>
                            </div>
                            <span class="muted">Starting value from appsettings: ${s.defaultWarningDays} days.</span>
                            ${this.#daysError ? html`<span class="error">${this.#daysError}</span>` : nothing}
                        </div>
                    </umb-property-layout>
                    ${this.#example()}
                </uui-box>

                <uui-box headline="Notifications">
                    <umb-property-layout
                        label="Email addresses"
                        description="Who gets the daily email when images need attention. One address per line. Nobody is listed by default, so no email is sent until you add an address or tick a user group below.">
                        <div slot="editor" class="field">
                            <uui-textarea label="Notification email addresses" .value=${this._emails} @input=${(e: Event) => (this._emails = (e.target as HTMLTextAreaElement).value)}></uui-textarea>
                            ${this.#emailError ? html`<span class="error">${this.#emailError}</span>` : nothing}
                            ${!this.#emailError && this.#uniqueEmails.length > 0 ? html`<span class="muted">${this.#uniqueEmails.length} ${this.#uniqueEmails.length === 1 ? "address" : "addresses"}</span>` : nothing}
                        </div>
                    </umb-property-layout>
                    <umb-property-layout
                        label="User groups"
                        description="Everyone in a ticked group also gets the email, at the address on their user account. A group is enough on its own: you do not need to list any addresses above. Disabled and locked-out users are skipped, and nobody gets it twice.">
                        <div slot="editor" class="field groups">
                            ${this._groups.length === 0
                                ? html`<span class="muted">No user groups found.</span>`
                                : this._groups.map(
                                      (g) => html`<uui-checkbox label=${g.name} .checked=${this._groupIds.includes(g.id)} @change=${(e: Event) => this.#toggleGroup(g.id, (e.target as HTMLInputElement).checked)}></uui-checkbox>`,
                                  )}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <div class="actions">
                    <span class="unsaved">${this.#dirty ? "You have unsaved changes" : ""}</span>
                    <uui-button class="action" look="primary" color="positive" label="Save settings" ?disabled=${this._saving || !this.#dirty || !this.#valid} @click=${this.#save}>
                        Save settings
                    </uui-button>
                </div>

                <uui-box headline="Set in appsettings (read-only)">
                    <p class="muted intro">
                        These are set by your developers in <code>appsettings.json</code>, under <code>Umbraco:Community:MediaGovernance</code>, and cannot be changed here.
                    </p>
                    <dl>
                        <dt>Hide withheld images in pages</dt>
                        <dd>${s.hideWithheldInPages ? "On: images that have expired, or whose people have not given consent, are left out of pages (using the <media-governance> tag helper)." : "Off: withheld images still show in pages."}</dd>
                        <dt>Block withheld image files</dt>
                        <dd>${s.blockWithheldFiles ? "On: the file of a withheld image returns a 404 to the public. Signed-in editors can still see it." : "Off: withheld images can still be reached by their address."}</dd>
                        <dt>Who can use Governance</dt>
                        <dd>${s.allowedUserGroups.length === 0 ? "No user groups." : s.allowedUserGroups.map((g) => html`<uui-tag look="secondary">${g}</uui-tag> `)}</dd>
                    </dl>
                </uui-box>
            </div>
        `;
    }

    static override styles = [buttonStyles, css`
        :host { display: block; padding: var(--uui-size-layout-1); }
        .page { display: flex; flex-direction: column; gap: var(--uui-size-layout-1); width: 100%; }
        uui-box { --uui-box-default-padding: var(--uui-size-space-6); }
        umb-property-layout { padding: var(--uui-size-space-4) 0; }
        umb-property-layout:first-of-type { padding-top: 0; }
        umb-property-layout:last-of-type { padding-bottom: 0; }
        .field { display: flex; flex-direction: column; gap: var(--uui-size-space-3); }
        .with-unit { display: flex; align-items: center; gap: var(--uui-size-space-3); }
        .with-unit span { color: var(--uui-color-text-alt); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
        .intro { margin-top: 0; }
        .error { color: var(--uui-color-danger-standalone); font-size: var(--uui-type-small-size); }
        .callout { padding: var(--uui-size-space-4) var(--uui-size-space-5); background: var(--uui-color-surface-alt); border-left: 4px solid var(--uui-color-warning); border-radius: var(--uui-border-radius); max-width: 560px; }
        .example { margin-top: var(--uui-size-space-5); padding: var(--uui-size-space-4) var(--uui-size-space-5); background: var(--uui-color-surface-alt); border-left: 3px solid var(--uui-color-interactive); border-radius: var(--uui-border-radius); }
        uui-textarea { width: 100%; max-width: 520px; }
        uui-input { max-width: 520px; width: 100%; }
        .with-unit uui-input { width: 120px; }
        .groups { gap: var(--uui-size-space-3); }
        dl { margin: 0; display: grid; grid-template-columns: 240px 1fr; gap: var(--uui-size-space-3) var(--uui-size-space-5); }
        dt { font-weight: 600; }
        dd { margin: 0; }
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
