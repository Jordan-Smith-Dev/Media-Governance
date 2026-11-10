import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "./overview-tab.element.js";
import "./people-tab.element.js";
import "./pages-tab.element.js";
import "./settings-tab.element.js";

type Tab = "overview" | "people" | "pages" | "settings";

@customElement("umb-media-governance-dashboard")
export class MediaGovernanceDashboardElement extends UmbLitElement {
    @state() private _tab: Tab = "overview";
    @state() private _personId = "";

    override connectedCallback() {
        super.connectedCallback();
        this.#hideSectionTabs();
        // ?person=<id> opens the People tab on that person (used by links from an image's consent records).
        const person = new URLSearchParams(window.location.search).get("person");
        if (person) {
            this._personId = person;
            this._tab = "people";
        }
    }

    #tab(tab: Tab, label: string, icon: string) {
        return html`
            <uui-tab label=${label} ?active=${this._tab === tab} @click=${() => (this._tab = tab)}>
                <uui-icon slot="icon" name=${icon}></uui-icon>${label}
            </uui-tab>
        `;
    }

    override render() {
        return html`
            <umb-body-layout>
                <div slot="header" class="headline">
                    <uui-tab-group>
                        <uui-tab label="Media" href="/umbraco/section/media/dashboard/media">Media</uui-tab>
                        <uui-tab label="Media governance" active>Media governance</uui-tab>
                    </uui-tab-group>
                </div>
                <uui-tab-group slot="navigation">
                    ${this.#tab("overview", "Overview", "icon-chart")} ${this.#tab("people", "People", "icon-users")}
                    ${this.#tab("pages", "Pages affected", "icon-document")} ${this.#tab("settings", "Settings", "icon-settings")}
                </uui-tab-group>
                <div class="body">
                    ${this._tab === "overview" ? html`<umb-media-governance-overview></umb-media-governance-overview>` : ""}
                    ${this._tab === "people" ? html`<umb-media-governance-people .focusPersonId=${this._personId}></umb-media-governance-people>` : ""}
                    ${this._tab === "pages" ? html`<umb-media-governance-pages></umb-media-governance-pages>` : ""}
                    ${this._tab === "settings" ? html`<umb-media-governance-settings></umb-media-governance-settings>` : ""}
                </div>
            </umb-body-layout>
        `;
    }

    // Umbraco puts a tab bar (Media | Media governance) above every dashboard. This dashboard has its own header, so
    // the bar is hidden while it is showing, to leave a single header. The header's "Media" link replaces the Media tab.
    #sectionTabs?: HTMLElement;

    #hideSectionTabs() {
        let node: Node | null = this;
        for (let depth = 0; node && depth < 25; depth++) {
            const parent: Node | null = node.parentNode;
            const host = parent instanceof ShadowRoot ? parent.host : null;
            if (host?.localName === "umb-section-main-views") {
                const tabs = (parent as ShadowRoot).querySelector<HTMLElement>("uui-tab-group#dashboards");
                // hide the whole header row (not just the tabs), or an empty bar is left behind
                const bar = tabs?.parentElement?.shadowRoot?.querySelector<HTMLElement>("#header");
                if (bar) {
                    bar.style.display = "none";
                    this.#sectionTabs = bar;
                }
                return;
            }
            node = host ?? parent;
        }
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        if (this.#sectionTabs) this.#sectionTabs.style.display = "";
        this.#sectionTabs = undefined;
    }

    static override styles = css`
        :host { display: block; height: 100%; }
        /* Laid out like the Media section's own tab bar: plain labels on the left, the dashboard's tabs on the right. */
        .headline { display: flex; align-items: center; height: 100%; }
        .body { padding: var(--uui-size-layout-1); }
    `;
}

export default MediaGovernanceDashboardElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-dashboard": MediaGovernanceDashboardElement;
    }
}
