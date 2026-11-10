import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import { STATUS_HELP, STATUS_LABELS, type AffectedPage } from "../api/index.js";
import { STATUS_COLORS } from "./format.js";

@customElement("umb-media-governance-pages")
export class MediaGovernancePagesElement extends GovernanceElement {
    @state() private _pages: AffectedPage[] = [];
    @state() private _loading = true;
    @state() private _error = "";

    override connectedCallback() {
        super.connectedCallback();
        void this.#load();
    }

    async #load() {
        this._loading = true;
        this._error = "";
        try {
            this._pages = await this.api.pagesAffected();
        } catch (e) {
            this._error = errorMessage(e);
        } finally {
            this._loading = false;
        }
    }

    override render() {
        if (this._error) return html`<uui-tag color="danger">${this._error}</uui-tag>`;
        if (this._loading) return html`<uui-loader></uui-loader>`;

        return html`
            <p class="intro">
                Pages that use an <strong>expired</strong> image or an image of someone who has <strong>not consented</strong>.
                Open the page to replace or remove the image.
            </p>
            ${this._pages.length === 0
                ? html`
                      <div class="empty">
                          <uui-icon name="icon-check"></uui-icon>
                          <p>No pages use withheld images.</p>
                          <p class="muted">Nothing needs fixing on the website right now.</p>
                      </div>
                  `
                : html`
                      <uui-table class="pages">
                          <uui-table-head>
                              <uui-table-head-cell>Page</uui-table-head-cell>
                              <uui-table-head-cell>Type</uui-table-head-cell>
                              <uui-table-head-cell>State</uui-table-head-cell>
                              <uui-table-head-cell>Withheld images on this page</uui-table-head-cell>
                          </uui-table-head>
                          ${this._pages.map(
                              (p) => html`
                                  <uui-table-row>
                                      <uui-table-cell>
                                          <a class="name" href="/umbraco/section/content/workspace/document/edit/${p.pageKey}">
                                              ${p.contentTypeIcon ? html`<uui-icon name=${p.contentTypeIcon}></uui-icon>` : nothing} ${p.name}
                                          </a>
                                      </uui-table-cell>
                                      <uui-table-cell>${p.contentType ?? html`<span class="muted">—</span>`}</uui-table-cell>
                                      <uui-table-cell>
                                          <uui-tag look="secondary" color=${p.published ? "positive" : "default"}>${p.published ? "Published" : "Draft"}</uui-tag>
                                      </uui-table-cell>
                                      <uui-table-cell>
                                          <div class="images">
                                              ${p.images.map(
                                                  (i) => html`
                                                      <a class="image" href="/umbraco/section/media/workspace/media/edit/${i.mediaKey}/invariant/view/governance">
                                                          <span>${i.name}</span>
                                                          <uui-tag color=${STATUS_COLORS[i.status]} title=${STATUS_HELP[i.status]}>${STATUS_LABELS[i.status]}</uui-tag>
                                                      </a>
                                                  `
                                              )}
                                          </div>
                                      </uui-table-cell>
                                  </uui-table-row>
                              `
                          )}
                      </uui-table>
                      <div class="footer">${this._pages.length} ${this._pages.length === 1 ? "page" : "pages"} affected</div>
                  `}
            <div class="note">
                <uui-icon name="icon-info"></uui-icon>
                <span>
                    This list uses Umbraco's <em>Referenced by</em> data, which includes media pickers, rich text editors and blocks.
                    It does <strong>not</strong> include images written directly into a template. Those are reported in the Umbraco log
                    when the page is viewed.
                </span>
            </div>
        `;
    }

    static override styles = css`
        :host { display: block; }
        .intro { margin: 0 0 var(--uui-size-space-5); color: var(--uui-color-text-alt); }
        .muted { color: var(--uui-color-text-alt); }
        .pages uui-table-cell, .pages uui-table-head-cell { padding: var(--uui-size-space-4) var(--uui-size-space-5); vertical-align: middle; }
        .pages uui-table-head-cell { color: var(--uui-color-text-alt); font-weight: 600; }
        .pages uui-table-row:hover { background: var(--uui-color-surface-alt); }
        .name { color: var(--uui-color-interactive); font-weight: 600; text-decoration: none; display: inline-flex; align-items: center; gap: var(--uui-size-space-2); }
        .name:hover { text-decoration: underline; }
        .images { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); }
        .image { display: inline-flex; align-items: center; gap: var(--uui-size-space-3); text-decoration: none; color: var(--uui-color-interactive); font-weight: 600; }
        .image span { line-height: 1; }
        .image:hover span { text-decoration: underline; }
        .footer { margin-top: var(--uui-size-space-5); color: var(--uui-color-text-alt); }
        .empty { display: flex; flex-direction: column; align-items: center; gap: var(--uui-size-space-3); padding: var(--uui-size-layout-2) 0; color: var(--uui-color-text-alt); }
        .empty uui-icon { font-size: 2rem; color: var(--uui-color-positive-standalone); }
        .note { display: flex; gap: var(--uui-size-space-3); align-items: flex-start; margin-top: var(--uui-size-space-6); color: var(--uui-color-text-alt); }
    `;
}

export default MediaGovernancePagesElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-pages": MediaGovernancePagesElement;
    }
}
