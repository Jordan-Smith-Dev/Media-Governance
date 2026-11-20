import type { UmbEntryPointOnInit } from "@umbraco-cms/backoffice/extension-api";
import { umbExtensionsRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { GovernanceApi } from "./api/index.js";

const ID = "Umbraco.Community.MediaGovernance";

/**
  * Umbraco's media card in the grid view shows no signs, so ours wraps it and adds them. It is only registered when the
  * signs or folder markers are switched on in appsettings, so by default the grid is Umbraco's own.
  */
const cardManifest: UmbExtensionManifest = {
  type: "entityCollectionItemCard",
  alias: `${ID}.MediaCard`,
  name: "Media Governance Media Card",
  element: () => import("./collection/governance-media-card.element.js"),
  weight: 100,
  forEntityTypes: ["media"],
};

/** Adds the grid card wrapper only when appsettings turns on the signs or folder markers. */
export const onInit: UmbEntryPointOnInit = async () => {
  try {
    const settings = await new GovernanceApi().settings();
    if ((settings.showStatusSigns || settings.showFolderMarkers) && !umbExtensionsRegistry.isRegistered(cardManifest.alias)) {
      umbExtensionsRegistry.register(cardManifest);
    }
  } catch {
    // No access to Governance (or not signed in yet): leave Umbraco's own cards in place.
  }
};

export const onUnload = () => {
  // nothing to clean up
};
