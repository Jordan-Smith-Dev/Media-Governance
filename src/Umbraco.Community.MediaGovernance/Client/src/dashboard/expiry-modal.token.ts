import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface ExpiryModalData {
    /** How many images are ticked. */
    count: number;
}

/** What the person chose: set one date on every image, or remove their own expiry. */
export type ExpiryModalValue = { action: "set"; date: string } | { action: "clear" };

export const EXPIRY_MODAL_ALIAS = "UmbracoCommunity.MediaGovernance.Modal.Expiry";

/** Umbraco's standard dialog modal. */
export const MEDIA_GOVERNANCE_EXPIRY_MODAL = new UmbModalToken<ExpiryModalData, ExpiryModalValue>(EXPIRY_MODAL_ALIAS, {
    modal: { type: "dialog", size: "small" },
});
