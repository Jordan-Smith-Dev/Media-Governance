import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import { STATUS_HELP, STATUS_LABELS, type GovernanceStatus, type OverviewSort, type OverviewItem, type OverviewPage, type Settings } from "../api/index.js";
import { STATUS_COLORS, formatDate, relativeDays } from "./format.js";

const PAGE_SIZE = 50;
const MAX_PEOPLE_SHOWN = 2;

const TILES: { status: GovernanceStatus | null; label: string; hint: string; icon: string }[] = [
    { status: null, label: "All images", hint: "In the media library", icon: "icon-pictures-alt" },
    { status: "NeedsTagging", label: "Needs tagging", hint: "People not recorded", icon: "icon-tag" },
    { status: "NoConsent", label: "No consent", hint: "People tagged, consent not given", icon: "icon-alert" },
    { status: "ExpiringSoon", label: "Expiring soon", hint: "Within the warning window", icon: "icon-time" },
    { status: "Expired", label: "Expired", hint: "Must stop being used", icon: "icon-block" },
];

const NO_PEOPLE_EXPIRED_HELP = "No people are pictured, so no consent is needed. The image has passed its expiry date, so it should not be used.";

const NEWLINES = "\n\n";

const SORTS: OverviewSort[] = ["Name", "Folder", "DateTaken", "Expiry", "Status"];

@customElement("umb-media-governance-overview")
export class MediaGovernanceOverviewElement extends GovernanceElement {
    @state() private _page?: OverviewPage;
    @state() private _settings?: Settings;
    @state() private _selected = new Set<string>();
    @state() private _status: GovernanceStatus | null = null;
    @state() private _filter = "";
    @state() private _sort: OverviewSort = "Name";
    @state() private _desc = false;
    @state() private _loading = true;
    @state() private _error = "";
    #debounce?: number;
    #request = 0;

    override connectedCallback() {
        super.connectedCallback();
        this.#readUrl();
        void this.#load(false);
        this.api.settings().then((s) => (this._settings = s)).catch(() => undefined);
    }

    async #load(append: boolean) {
        const request = ++this.#request; // ignore responses that arrive after a newer search
        this._loading = true;
        this._error = "";
        try {
            const skip = append && this._page ? this._page.items.length : 0;
            const page = await this.api.overview(this._status, this._filter, skip, PAGE_SIZE, this._sort, this._desc);
            if (request !== this.#request) return;
            this._page = append && this._page ? { ...page, items: [...this._page.items, ...page.items] } : page;
        } catch (e) {
            if (request === this.#request) this._error = errorMessage(e);
        } finally {
            if (request === this.#request) this._loading = false;
        }
    }

    // The view (status, search, sort) lives in the address, so it can be bookmarked or shared.
    #readUrl() {
        const q = new URLSearchParams(window.location.search);
        const status = q.get("status") as GovernanceStatus | null;
        if (status && status in STATUS_LABELS) this._status = status;
        this._filter = q.get("q") ?? "";
        const sort = q.get("sort") as OverviewSort | null;
        if (sort && SORTS.includes(sort)) {
            this._sort = sort;
            this._desc = q.get("desc") === "true";
        }
    }

    #syncUrl() {
        const q = new URLSearchParams(window.location.search); // keeps unrelated parameters such as ?person=
        const set = (key: string, value: string | null) => (value ? q.set(key, value) : q.delete(key));
        set("status", this._status);
        set("q", this._filter.trim() || null);
        set("sort", this._sort === "Name" && !this._desc ? null : this._sort);
        set("desc", this._desc ? "true" : null);
        const qs = q.toString();
        window.history.replaceState(window.history.state, "", `${window.location.pathname}${qs ? `?${qs}` : ""}${window.location.hash}`);
    }

    #sortBy(column: OverviewSort) {
        if (this._sort === column) this._desc = !this._desc;
        else {
            this._sort = column;
            this._desc = false;
        }
        this.#syncUrl();
        void this.#load(false);
    }

    #sortHeader(column: OverviewSort, label: string) {
        const active = this._sort === column;
        return html`<button class="sort ${active ? "active" : ""}" title=${`Sort by ${label.toLowerCase()}`} @click=${() => this.#sortBy(column)}>
            ${label}<span class="arrow" aria-hidden="true">${active ? (this._desc ? "▼" : "▲") : "↕"}</span>
        </button>`;
    }

    #ariaSort(column: OverviewSort) {
        return this._sort === column ? (this._desc ? "descending" : "ascending") : "none";
    }

    #selectTile(status: GovernanceStatus | null) {
        this._status = this._status === status ? null : status; // clicking the active tile clears it
        this.#syncUrl();
        void this.#load(false);
    }

    #onFilter(e: Event) {
        this._filter = (e.target as HTMLInputElement).value;
        window.clearTimeout(this.#debounce);
        this.#debounce = window.setTimeout(() => {
            this.#syncUrl();
            void this.#load(false);
        }, 250);
    }

    #clearAll() {
        this._status = null;
        this._filter = "";
        this.#syncUrl();
        void this.#load(false);
    }

    async #export(selectedOnly: boolean) {
        try {
            await this.api.exportCsv(this._status, this._filter, selectedOnly ? [...this._selected] : undefined, this._sort, this._desc);
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    #toggle(key: string, checked: boolean) {
        const next = new Set(this._selected);
        if (checked) next.add(key);
        else next.delete(key);
        this._selected = next;
    }

    #toggleAll(checked: boolean) {
        const next = new Set(this._selected);
        for (const i of this._page?.items ?? []) {
            if (checked) next.add(i.mediaKey);
            else next.delete(i.mediaKey);
        }
        this._selected = next;
    }

    #count(status: GovernanceStatus | null) {
        if (!this._page) return "–";
        return status === null ? this._page.allCount : (this._page.counts[status] ?? 0);
    }

    override render() {
        const filtering = this._status !== null || this._filter.trim() !== "";
        return html`
            <div class="tiles">
                ${TILES.map(
                    (t) => html`
                        <button
                            class="tile ${t.status ?? "All"} ${this._status === t.status ? "active" : ""}"
                            aria-pressed=${this._status === t.status}
                            @click=${() => this.#selectTile(t.status)}>
                            <span class="tile-top">
                                <span class="tile-label">${t.label}</span>
                                <uui-icon name=${t.icon}></uui-icon>
                            </span>
                            <span class="tile-count">${this.#count(t.status)}</span>
                            <span class="tile-hint">${t.status === "ExpiringSoon" && this._settings ? `Within ${this._settings.warningDays} days` : t.hint}</span>
                        </button>
                    `
                )}
            </div>

            <div class="toolbar">
                <uui-input label="Filter images" placeholder="Filter by image, folder or person..." .value=${this._filter} @input=${this.#onFilter}>
                    <uui-icon name="icon-search" slot="prepend"></uui-icon>
                </uui-input>
                ${this._selected.size > 0
                    ? html`<uui-button class="action" look="outline" label="Clear selection" @click=${() => (this._selected = new Set())}>Clear selection</uui-button>`
                    : nothing}
                <uui-button class="action" look="primary" label=${this._selected.size > 0 ? "Export selected" : "Export all matching"}
                    title=${this._selected.size > 0 ? "Exports only the images you have ticked" : "Exports every image matching the current filter. Tick images to export only those."}
                    @click=${() => this.#export(this._selected.size > 0)}>
                    <uui-icon name="icon-download-alt"></uui-icon> ${this._selected.size > 0 ? `Export selected (${this._selected.size})` : "Export"}
                </uui-button>
            </div>

            ${filtering
                ? html`
                      <div class="active-filters">
                          <span class="muted">Showing</span>
                          ${this._status ? html`<uui-tag color=${STATUS_COLORS[this._status]} title=${STATUS_HELP[this._status]}>${STATUS_LABELS[this._status]}</uui-tag>` : nothing}
                          ${this._filter.trim() ? html`<uui-tag look="secondary">“${this._filter.trim()}”</uui-tag>` : nothing}
                          <uui-button class="action" compact look="secondary" label="Clear filters" @click=${this.#clearAll}>Clear filters</uui-button>
                      </div>
                  `
                : nothing}
            ${this._error ? html`<uui-tag color="danger">${this._error}</uui-tag>` : nothing} ${this.#renderTable()}
        `;
    }

    #renderPeople(item: OverviewItem) {
        if (item.people.length === 0)
            return item.noPeople
                ? html`<span class="muted">No people</span>`
                : html`<span class="untagged" title="Nobody is tagged and the image is not marked as having no people">Not tagged yet</span>`;
        const shown = item.people.slice(0, MAX_PEOPLE_SHOWN);
        const more = item.people.length - shown.length;
        return html`
            <div class="chips" title=${item.people.join(", ")}>
                ${shown.map((p) => html`<uui-tag look="secondary">${p}</uui-tag>`)}
                ${more > 0 ? html`<uui-tag look="placeholder" title=${item.people.slice(MAX_PEOPLE_SHOWN).join(", ")}>+${more}</uui-tag>` : nothing}
            </div>
        `;
    }

    #renderConsent(item: OverviewItem) {
        if (item.consent === "Given") return html`<uui-tag color="positive" look="primary">Given</uui-tag>`;
        if (item.consent === "Not given") return html`<uui-tag color="danger" look="primary">Not given</uui-tag>`;
        // nobody is pictured, so there is nothing to consent to; or nobody has been tagged yet, so it is not known
        return html`<span class="muted">${item.noPeople ? "Not required" : "Unknown"}</span>`;
    }

    #renderTable() {
        if (!this._page) return this._loading ? html`<uui-loader></uui-loader>` : nothing;
        if (this._page.items.length === 0) {
            return html`
                <div class="empty">
                    <uui-icon name="icon-search"></uui-icon>
                    <p>No images match your filters.</p>
                    <p class="muted">Try a different status or search, or clear the filters.</p>
                </div>
            `;
        }

        return html`
            <uui-table class="images">
                <uui-table-head>
                    <uui-table-head-cell aria-sort=${this.#ariaSort("Name")}>
                        <uui-checkbox aria-label="Select all shown" .checked=${(this._page?.items.length ?? 0) > 0 && this._page!.items.every((i) => this._selected.has(i.mediaKey))} @change=${(e: Event) => this.#toggleAll((e.target as HTMLInputElement).checked)}></uui-checkbox>
                        ${this.#sortHeader("Name", "Name")}
                    </uui-table-head-cell>
                    <uui-table-head-cell aria-sort=${this.#ariaSort("Folder")}>${this.#sortHeader("Folder", "Folder")}</uui-table-head-cell>
                    <uui-table-head-cell>People</uui-table-head-cell>
                    <uui-table-head-cell aria-sort=${this.#ariaSort("DateTaken")}>${this.#sortHeader("DateTaken", "Date taken")}</uui-table-head-cell>
                    <uui-table-head-cell aria-sort=${this.#ariaSort("Expiry")}>${this.#sortHeader("Expiry", "Expiry date")}</uui-table-head-cell>
                    <uui-table-head-cell>Consent</uui-table-head-cell>
                    <uui-table-head-cell aria-sort=${this.#ariaSort("Status")}>${this.#sortHeader("Status", "Status")}</uui-table-head-cell>
                </uui-table-head>
                ${this._page.items.map(
                    (i) => html`
                        <uui-table-row>
                            <uui-table-cell>
                                <uui-checkbox aria-label=${`Select ${i.name}`} .checked=${this._selected.has(i.mediaKey)} @change=${(e: Event) => this.#toggle(i.mediaKey, (e.target as HTMLInputElement).checked)}></uui-checkbox>
                                <a class="name" href="/umbraco/section/media/workspace/media/edit/${i.mediaKey}/invariant/view/governance" title="Open the governance details for ${i.name}">${i.name}</a>
                            </uui-table-cell>
                            <uui-table-cell>${i.folder || html`<span class="muted">Media root</span>`}</uui-table-cell>
                            <uui-table-cell>${this.#renderPeople(i)}</uui-table-cell>
                            <uui-table-cell>${formatDate(i.dateTaken)}</uui-table-cell>
                            <uui-table-cell>
                                ${formatDate(i.expiry)}
                                ${i.daysToExpiry !== null
                                    ? html`<div class="sub ${i.daysToExpiry <= 0 ? "late" : ""}">${i.daysToExpiry < 0 ? "Expired " : ""}${relativeDays(i.daysToExpiry)}${i.expirySource === "Custom" ? " · custom" : i.expirySource === "Consent" ? " · consent" : ""}</div>`
                                    : nothing}
                            </uui-table-cell>
                            <uui-table-cell>${this.#renderConsent(i)}</uui-table-cell>
                            <uui-table-cell>
                                <div class="statuses">
                                    <uui-tag color=${STATUS_COLORS[i.status]} title=${i.status === "Expired" && i.noPeople ? NO_PEOPLE_EXPIRED_HELP : STATUS_HELP[i.status]}>${STATUS_LABELS[i.status]}</uui-tag>
                                    ${i.issues.length > 1 ? html`<uui-tag look="secondary" title=${i.issues.filter((s) => s !== i.status).map((s) => `${STATUS_LABELS[s]}: ${STATUS_HELP[s]}`).join(NEWLINES)}>+${i.issues.length - 1}</uui-tag>` : nothing}
                                </div>
                            </uui-table-cell>
                        </uui-table-row>
                    `
                )}
            </uui-table>
            <div class="footer">
                <span>Showing ${this._page.items.length} of ${this._page.total}</span>
                ${this._page.items.length < this._page.total
                    ? html`<uui-button class="action" label="Load more" look="secondary" ?disabled=${this._loading} @click=${() => this.#load(true)}>Load more</uui-button>`
                    : nothing}
            </div>
            ${this._settings
                ? html`<p class="retention-note">Images expire ${this._settings.retentionYears} years after the date taken unless a custom expiry is set.</p>`
                : nothing}
        `;
    }

    static override styles = [buttonStyles, css`
        :host { display: block; }
        .muted { color: var(--uui-color-text-alt); }

        /* Filter tiles */
        .tiles { display: grid; grid-template-columns: repeat(5, 1fr); gap: var(--uui-size-space-5); margin-bottom: var(--uui-size-layout-1); }
        .tile { all: unset; box-sizing: border-box; cursor: pointer; display: flex; flex-direction: column; gap: var(--uui-size-space-2); padding: var(--uui-size-space-5); background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); border-left: 4px solid var(--tile-accent, var(--uui-color-border-emphasis)); border-radius: var(--uui-border-radius); transition: box-shadow 120ms, border-color 120ms; }
        .tile:hover { box-shadow: var(--uui-shadow-depth-1); border-color: var(--uui-color-border-emphasis); border-left-color: var(--tile-accent, var(--uui-color-border-emphasis)); }
        .tile:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
        .tile.active { background: var(--uui-color-surface-alt); box-shadow: 0 0 0 2px var(--uui-color-selected); border-color: var(--uui-color-selected); border-left-color: var(--tile-accent, var(--uui-color-selected)); }
        .tile.All { --tile-accent: var(--uui-color-selected); }
        .tile.NeedsTagging, .tile.ExpiringSoon { --tile-accent: var(--uui-color-warning-standalone); }
        .tile.NoConsent, .tile.Expired { --tile-accent: var(--uui-color-danger-standalone); }
        .tile-top { display: flex; justify-content: space-between; align-items: center; color: var(--uui-color-text-alt); }
        .tile-label { font-weight: 600; color: var(--uui-color-text); }
        .tile-count { font-size: 2.2rem; line-height: 1.1; font-weight: 700; color: var(--tile-accent, var(--uui-color-text)); }
        .tile.All .tile-count { color: var(--uui-color-text); }
        .tile-hint { font-size: var(--uui-type-small-size); color: var(--uui-color-text-alt); }

        /* Filter bar */
        .toolbar { display: flex; gap: var(--uui-size-space-4); margin-bottom: var(--uui-size-space-4); }
        .toolbar uui-input { flex: 1; }
        .toolbar uui-icon[slot="prepend"] { margin: 0 var(--uui-size-space-2) 0 var(--uui-size-space-3); color: var(--uui-color-text-alt); }
        .active-filters { display: flex; align-items: center; flex-wrap: wrap; gap: var(--uui-size-space-3); margin-bottom: var(--uui-size-space-4); }

        /* Table */
        .images uui-table-cell, .images uui-table-head-cell { padding: var(--uui-size-space-4) var(--uui-size-space-5); }
        .images uui-table-head-cell { color: var(--uui-color-text-alt); font-weight: 600; }
        .sort { all: unset; cursor: pointer; display: inline-flex; align-items: center; gap: var(--uui-size-space-2); font-weight: 600; color: inherit; }
        .sort:hover, .sort.active { color: var(--uui-color-interactive-emphasis); }
        .sort:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; border-radius: 2px; }
        .sort .arrow { font-size: 0.75em; opacity: 0.6; }
        .sort.active .arrow { opacity: 1; }
        .images uui-table-row:hover { background: var(--uui-color-surface-alt); }
        .images uui-table-cell { vertical-align: middle; }
        .name { color: var(--uui-color-interactive); font-weight: 600; text-decoration: none; }
        .name:hover { color: var(--uui-color-interactive-emphasis); text-decoration: underline; }
        .untagged { color: var(--uui-color-warning-standalone); font-weight: 600; }
        uui-checkbox { margin-right: var(--uui-size-space-3); }
        .statuses { display: flex; gap: var(--uui-size-space-2); align-items: center; }
        .chips { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
        .sub { font-size: var(--uui-type-small-size); color: var(--uui-color-text-alt); margin-top: 2px; }
        .sub.late { color: var(--uui-color-danger-standalone); }
        .retention-note { margin: var(--uui-size-space-4) 0 0; color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
        .footer { display: flex; justify-content: space-between; align-items: center; margin-top: var(--uui-size-space-5); color: var(--uui-color-text-alt); }
        .empty { display: flex; flex-direction: column; align-items: center; gap: var(--uui-size-space-3); padding: var(--uui-size-layout-2) 0; color: var(--uui-color-text-alt); }
        .empty uui-icon { font-size: 2rem; }
        @media (max-width: 1100px) { .tiles { grid-template-columns: repeat(2, 1fr); } }
    `];
}

export default MediaGovernanceOverviewElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-overview": MediaGovernanceOverviewElement;
    }
}
