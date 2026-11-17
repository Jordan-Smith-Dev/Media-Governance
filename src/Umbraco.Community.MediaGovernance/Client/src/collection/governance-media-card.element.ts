import { css, html, customElement, property, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbCollectionItemModel } from "@umbraco-cms/backoffice/collection";
import { umbExtensionsRegistry } from "@umbraco-cms/backoffice/extension-registry";

const UMBRACO_MEDIA_CARD_ALIAS = "Umb.EntityCollectionItemCard.Media";

type CardItem = UmbCollectionItemModel & { flags?: Array<{ alias: string }> };

/**
 * Umbraco's own media card in the collection (grid) view, with governance signs added in the corner of its name bar.
 * Umbraco's card does not show entity signs, so this wraps it rather than replacing it.
 */
@customElement("umb-media-governance-card")
export class MediaGovernanceCardElement extends UmbLitElement {
    @property({ type: Object, attribute: false }) item?: CardItem;
    @property() href?: string;
    @property({ type: Boolean }) selectable = false;
    @property({ type: Boolean }) selected = false;
    @property({ type: Boolean, attribute: "select-only" }) selectOnly = false;
    @property({ type: Boolean }) disabled = false;

    override connectedCallback() {
        super.connectedCallback();
        // make sure Umbraco's card element is defined before we render it
        const manifest = umbExtensionsRegistry.getByAlias(UMBRACO_MEDIA_CARD_ALIAS) as { element?: () => Promise<unknown> } | undefined;
        void manifest?.element?.().then(() => this.requestUpdate());
    }

    override render() {
        if (!this.item) return nothing;
        return html`
            <umb-media-collection-item-card
                .item=${this.item}
                .href=${this.href}
                ?selectable=${this.selectable}
                ?selected=${this.selected}
                ?select-only=${this.selectOnly}
                ?disabled=${this.disabled}>
                <slot name="actions" slot="actions"></slot>
            </umb-media-collection-item-card>
            ${this.item.flags?.length
                ? html`<umb-entity-sign-bundle .entityType=${this.item.entityType} .entityFlags=${this.item.flags}></umb-entity-sign-bundle>`
                : nothing}
        `;
    }

    // The sign bundle has no size of its own: its icons are placed absolutely, 10px down and 12px right of its
    // top-left corner, at 8px (sized for a tree row). We scale it up and anchor that corner so the icons land in the
    // right-hand end of the card's name bar, clear of the name text.
    static override styles = css`
        :host {
            display: block;
            position: relative;
            height: 100%;
        }
        umb-media-collection-item-card {
            height: 100%;
        }
        umb-entity-sign-bundle {
            position: absolute;
            top: calc(100% - 44px);
            left: calc(100% - 48px);
            transform: scale(1.8);
            transform-origin: top left;
            z-index: 1;
            --umb-sign-bundle-bg: var(--uui-color-surface);
        }
    `;
}

export default MediaGovernanceCardElement;
export { MediaGovernanceCardElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-card": MediaGovernanceCardElement;
    }
}
