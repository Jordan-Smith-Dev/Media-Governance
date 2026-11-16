import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

import { GOVERNANCE_ENTITY_TYPE, GOVERNANCE_WORKSPACE_ALIAS } from "./constants.js";

export const GOVERNANCE_WORKSPACE_CONTEXT = new UmbContextToken<MediaGovernanceWorkspaceContext>("UmbMediaGovernanceWorkspaceContext");

/** The "Governance" page in the Media section. Like the Log Viewer, it is one workspace whose views are the header tabs. */
export class MediaGovernanceWorkspaceContext extends UmbContextBase {
    // The workspace-alias condition on each tab reads this.
    workspaceAlias = GOVERNANCE_WORKSPACE_ALIAS;

    constructor(host: UmbControllerHost) {
        super(host, GOVERNANCE_WORKSPACE_CONTEXT);
    }

    getEntityType() {
        return GOVERNANCE_ENTITY_TYPE;
    }
}

export { MediaGovernanceWorkspaceContext as api };
