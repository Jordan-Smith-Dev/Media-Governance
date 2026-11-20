import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import type { AdminValues, Settings, UserGroup } from "../api/index.js";
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
  * window and the notification recipients can be changed here. Everything else is appsettings only, and is not shown to editors.
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
  // Admin settings (only present for admins, and only while switched on in appsettings)
  @state() private _admin?: AdminValues;
  @state() private _adminDefaults?: AdminValues;
  @state() private _adminUses = "";
  @state() private _adminBaseline = "";
  @state() private _adminSaving = false;

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
      if (s.adminSettings) {
        this._admin = { ...s.adminSettings.current };
        this._adminDefaults = s.adminSettings.defaults;
        this._adminUses = s.adminSettings.current.permittedUses.join(", ");
        this._adminBaseline = this.#adminSnapshot();
      } else {
        this._admin = undefined;
      }
    } catch (e) {
      this._error = errorMessage(e);
    }
  }

  #snapshot() {
    return JSON.stringify([this._years, this._days, splitList(this._emails), [...this._groupIds].sort()]);
  }

  // ---------------------------------------------------------------- admin settings

  /** Removes repeats, ignoring case, keeping the first spelling. */
  #unique(list: string[]) {
    const seen = new Set<string>();
    return list.filter((v) => !seen.has(v.toLowerCase()) && seen.add(v.toLowerCase()));
  }

  #adminSnapshot() {
    return JSON.stringify([this._admin ? { ...this._admin, permittedUses: [] } : null, this.#adminUsesList]);
  }

  get #adminUsesList() {
    return this.#unique(splitList(this._adminUses));
  }

  get #adminDirty() {
    return this.#adminSnapshot() !== this._adminBaseline;
  }

  get #adminUsesError() {
    const uses = this.#adminUsesList;
    if (uses.length > 12) return "Use at most 12 permitted uses.";
    const tooLong = uses.find((u) => u.length > 30);
    return tooLong ? `"${tooLong.slice(0, 20)}…" is too long: keep each use under 30 characters.` : "";
  }

  #setAdmin(change: Partial<AdminValues>) {
    if (this._admin) this._admin = { ...this._admin, ...change };
  }

  /** True when the value in force differs from what appsettings says. */
  #changedHere(key: keyof AdminValues) {
    const d = this._adminDefaults;
    if (!d || !this._admin) return false;
    if (key === "permittedUses") {
      const a = this.#adminUsesList.map((u) => u.toLowerCase()).sort().join("|");
      const b = d.permittedUses.map((u) => u.toLowerCase()).sort().join("|");
      return a !== b;
    }
    return this._admin[key] !== d[key];
  }

  #reset(key: keyof AdminValues) {
    const d = this._adminDefaults;
    if (!d) return;
    if (key === "permittedUses") this._adminUses = d.permittedUses.join(", ");
    else this.#setAdmin({ [key]: d[key] } as Partial<AdminValues>);
  }

  async #saveAdmin() {
    const a = this._admin;
    const before = this.#adminBefore;
    if (!a || this.#adminUsesError) return;

    // The two protective switches are worth a second look before they are turned off.
    const turnedOff: string[] = [];
    if (before?.blockWithheldFiles && !a.blockWithheldFiles) turnedOff.push("withheld image files will be served to the public again");
    if (before?.hideWithheldInPages && !a.hideWithheldInPages) turnedOff.push("withheld images will show in pages again");
    if (turnedOff.length > 0) {
      try {
        await umbConfirmModal(this, {
          headline: "Switch off a protection?",
          content: `If you save this, ${turnedOff.join(" and ")}. Expired images, and images of people without consent, will be reachable by the public.`,
          color: "danger",
          confirmLabel: "Switch off and save",
        });
      } catch {
        return; // cancelled
      }
    }

    this._adminSaving = true;
    try {
      await this.api.saveAdminSettings({ ...a, permittedUses: this.#adminUsesList });
      await this.#load();
      this.notify("positive", "Admin settings saved");
    } catch (e) {
      this.notify("danger", errorMessage(e));
    } finally {
      this._adminSaving = false;
    }
  }

  /** The admin values as they were when the screen loaded (what is in force), to tell which protections are being switched off. */
  get #adminBefore(): AdminValues | undefined {
    return this._settings?.adminSettings?.current;
  }

  #renderOverride(key: keyof AdminValues, defaultText: string) {
    if (!this.#changedHere(key)) return nothing;
    return html`<div class="override">
      <uui-tag look="secondary" color="warning">Changed here</uui-tag>
      <span class="muted">appsettings says: ${defaultText}</span>
      <uui-button compact look="default" label="Reset to appsettings" @click=${() => this.#reset(key)}>Reset</uui-button>
    </div>`;
  }

  #renderToggle(key: Exclude<keyof AdminValues, "permittedUses">, label: string, description: string, on = "On", off = "Off") {
    const a = this._admin!;
    const d = this._adminDefaults!;
    return html`
      <umb-property-layout label=${label} description=${description}>
        <div slot="editor" class="field">
          <uui-toggle label=${label} .checked=${a[key]} @change=${(e: Event) => this.#setAdmin({ [key]: (e.target as HTMLInputElement).checked } as Partial<AdminValues>)}>
            ${a[key] ? on : off}
          </uui-toggle>
          ${this.#renderOverride(key, d[key] ? on : off)}
        </div>
      </umb-property-layout>
    `;
  }

  #renderAdmin() {
    if (!this._admin || !this._adminDefaults) return nothing;
    const d = this._adminDefaults;
    return html`
      <div class="admin">
        <div class="admin-note">
          <strong>Admin settings.</strong> Only administrators see this. These change what <code>appsettings.json</code> says for this site, without a deployment.
          Anything changed here is marked, and <em>Reset</em> puts it back to what appsettings says. Who can use Governance, and where evidence is kept, can only be changed in appsettings.
        </div>

        <uui-box headline="Consent detail">
          ${this.#renderToggle("consentDetails", "Date given and who gave it", "Record when consent was given and who gave it: the person, a parent or guardian, or a legal representative.", "Recorded", "Not recorded")}
          ${this.#renderToggle("consentExpiry", "Consent expiry", "Let a consent end on a date. Once it passes, the person's images become Expired.", "Allowed", "Not used")}
          ${this.#renderToggle("consentEvidence", "Evidence upload", "Attach a signed form or email to a person's consent. It is kept in a private folder and every view is recorded.", "On", "Off")}
          <umb-property-layout
            label="Permitted uses"
            description="Separate them with commas. When there are any, each consent must say which uses it covers, and images show which uses everyone pictured has agreed to. Leave empty to record consent without uses. Consents already recorded are not changed.">
            <div slot="editor" class="field">
              <uui-input label="Permitted uses" .value=${this._adminUses} @input=${(e: Event) => (this._adminUses = (e.target as HTMLInputElement).value)}></uui-input>
              <div class="chips">${this.#adminUsesList.map((u) => html`<uui-tag look="secondary">${u}</uui-tag>`)}</div>
              ${this.#adminUsesError ? html`<span class="error">${this.#adminUsesError}</span>` : nothing}
              ${this.#renderOverride("permittedUses", d.permittedUses.length > 0 ? d.permittedUses.join(", ") : "none")}
            </div>
          </umb-property-layout>
        </uui-box>

        <uui-box headline="Your website">
          ${this.#renderToggle("hideWithheldInPages", "Hide withheld images in pages", "Pages using the <media-governance> tag helper leave out images that have expired, or whose people have not given consent. When off, they still show (and are still logged).", "Hidden", "Still shown")}
          ${this.#renderToggle("blockWithheldFiles", "Block withheld image files", "Anyone requesting the file of a withheld image gets a 404, even with the address. Signed-in editors can still see it. When off, only pages are affected.", "Blocked", "Still served")}
          ${!this._admin.blockWithheldFiles ? html`<div class="warn-line">Withheld images can still be reached by their address, and may still appear in search engines.</div>` : nothing}
        </uui-box>

        <uui-box headline="Media tree">
          ${this.#renderToggle("showStatusSigns", "Status signs on images", "Small indicators on images in the media tree and collection, one for each problem.", "Shown", "Hidden")}
          ${this.#renderToggle("colourTreeIcons", "Coloured image icons", "Colours each image's icon in the media tree by its status.", "Coloured", "Plain")}
          ${this.#renderToggle("showFolderMarkers", "Folder markers", "A red or amber marker on folders that contain images that cannot be used or need attention.", "Shown", "Hidden")}
        </uui-box>

        <div class="actions">
          <span class="unsaved">${this.#adminDirty ? "You have unsaved changes" : ""}</span>
          <uui-button class="action" look="primary" color="positive" label="Save admin settings" ?disabled=${this._adminSaving || !this.#adminDirty || !!this.#adminUsesError} @click=${this.#saveAdmin}>
            Save admin settings
          </uui-button>
        </div>
      </div>
    `;
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

        ${this.#renderAdmin()}
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
    .chips { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
    .admin { display: flex; flex-direction: column; gap: var(--uui-size-layout-1); }
    .admin-note { padding: var(--uui-size-space-4) var(--uui-size-space-5); background: var(--uui-color-surface-alt); border-left: 4px solid var(--uui-color-interactive); border-radius: var(--uui-border-radius); }
    .override { display: flex; align-items: center; gap: var(--uui-size-space-3); flex-wrap: wrap; }
    .warn-line { color: var(--uui-color-danger-standalone); font-size: var(--uui-type-small-size); }
    .error { color: var(--uui-color-danger-standalone); font-size: var(--uui-type-small-size); }
    .callout { padding: var(--uui-size-space-4) var(--uui-size-space-5); background: var(--uui-color-surface-alt); border-left: 4px solid var(--uui-color-warning); border-radius: var(--uui-border-radius); max-width: 560px; }
    .example { margin-top: var(--uui-size-space-5); padding: var(--uui-size-space-4) var(--uui-size-space-5); background: var(--uui-color-surface-alt); border-left: 3px solid var(--uui-color-interactive); border-radius: var(--uui-border-radius); }
    uui-textarea { width: 100%; max-width: 520px; }
    uui-input { max-width: 520px; width: 100%; }
    .with-unit uui-input { width: 120px; }
    .groups { gap: var(--uui-size-space-3); }
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
