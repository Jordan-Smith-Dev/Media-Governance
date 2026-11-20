import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { Person } from "../api/index.js";

export interface PersonModalData {
  /** The person to edit, or null to add a new one. */
  person: Person | null;
}

export interface PersonModalValue {
  /** True when something was saved or deleted, so the list needs reloading. */
  changed: boolean;
}

export const PERSON_MODAL_ALIAS = "UmbracoCommunity.MediaGovernance.Modal.Person";

/** Umbraco's standard sidebar (slide-out) modal. */
export const MEDIA_GOVERNANCE_PERSON_MODAL = new UmbModalToken<PersonModalData, PersonModalValue>(PERSON_MODAL_ALIAS, {
  modal: { type: "sidebar", size: "medium" },
});
