import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { ExpiryModalData, ExpiryModalValue } from "./expiry-modal.token.js";

/** Sets one expiry date on the ticked images, or removes their own expiry. */
@customElement("umb-media-governance-expiry-modal")
export class MediaGovernanceExpiryModalElement extends UmbModalBaseElement<ExpiryModalData, ExpiryModalValue> {
  @state() private _date = "";
  @state() private _error = "";

  #set() {
    if (!this._date) {
      this._error = "Choose an expiry date.";
      return;
    }
    this.value = { action: "set", date: this._date };
    this._submitModal();
  }

  #clear() {
    this.value = { action: "clear" };
    this._submitModal();
  }

  override render() {
    const n = this.data?.count ?? 0;
    return html`
      <umb-body-layout headline="Set expiry date for ${n} image${n === 1 ? "" : "s"}">
        <p class="muted">This replaces any expiry of their own the images have now. Without one, an image expires after the retention period set in Settings.</p>
        <umb-input-date type="date" label="Expiry date" .value=${this._date} @change=${(e: Event) => { this._date = (e.target as HTMLInputElement).value; this._error = ""; }}></umb-input-date>
        ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : ""}
        <div slot="actions">
          <uui-button class="action remove" look="secondary" label="Remove expiry" title="Go back to the retention period" @click=${this.#clear}>Remove expiry</uui-button>
          <uui-button class="action" look="secondary" label="Cancel" @click=${this._rejectModal}>Cancel</uui-button>
          <uui-button class="action" look="primary" label="Set expiry" @click=${this.#set}>Set expiry</uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  static override styles = [buttonStyles, css`
    .muted { color: var(--uui-color-text-alt); margin-top: 0; }
    .error { color: var(--uui-color-danger-standalone); font-size: var(--uui-type-small-size); }
    .remove { margin-right: auto; }
  `];
}

export default MediaGovernanceExpiryModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "umb-media-governance-expiry-modal": MediaGovernanceExpiryModalElement;
  }
}
