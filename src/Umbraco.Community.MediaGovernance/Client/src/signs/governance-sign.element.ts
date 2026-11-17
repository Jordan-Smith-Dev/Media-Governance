import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

type SignManifest = Extract<UmbExtensionManifest, { type: "entitySign" }>;
type SignMeta = { iconName?: string; iconColorAlias?: string };

/**
 * The status sign shown on a media tree item. It looks like Umbraco's own icon sign, but when its row is the
 * selected one it drops its colour, so it matches the picture icon (which Umbraco also de-colours on the active row).
 */
@customElement("umb-media-governance-sign")
export class MediaGovernanceSignElement extends UmbLitElement {
    @property({ type: Object, attribute: false }) manifest?: SignManifest;
    @state() private _active = false;

    #observer?: MutationObserver;

    override connectedCallback() {
        super.connectedCallback();
        const row = this.#findTreeRow();
        if (!row) return;

        const update = () => (this._active = row.hasAttribute("active") || row.hasAttribute("selected"));
        update();
        this.#observer = new MutationObserver(update);
        this.#observer.observe(row, { attributes: true, attributeFilter: ["active", "selected"] });
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        this.#observer?.disconnect();
        this.#observer = undefined;
    }

    /** Walks up through shadow roots to the tree row (a `uui-menu-item`) that this sign sits in. */
    #findTreeRow(): Element | undefined {
        let node: Node | null = this;
        for (let depth = 0; node && depth < 25; depth++) {
            if (node instanceof Element && node.localName === "uui-menu-item") return node;
            const parent: Node | null = (node as Node).parentNode;
            node = parent instanceof ShadowRoot ? parent.host : parent;
        }
        return undefined;
    }

    override render() {
        if (!this.manifest) return nothing;
        const meta = this.manifest.meta as SignMeta;
        return html`<umb-icon
            .name=${meta.iconName ?? "icon-circle-dotted"}
            .color=${this._active ? undefined : meta.iconColorAlias}></umb-icon>`;
    }

    // Same halo as Umbraco's own icon signs, so the sign stays readable against the row background.
    static override styles = css`
        umb-icon {
            filter: drop-shadow(-1px 0 0 var(--umb-sign-bundle-bg)) drop-shadow(0 -1px 0 var(--umb-sign-bundle-bg))
                drop-shadow(0 1px 0 var(--umb-sign-bundle-bg));
        }
        umb-icon::before {
            content: "";
            position: absolute;
            z-index: -1;
            border-radius: 50%;
            inset: 2px;
            background-color: var(--umb-sign-bundle-bg);
        }
    `;
}

export default MediaGovernanceSignElement;
export { MediaGovernanceSignElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-sign": MediaGovernanceSignElement;
    }
}
