import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbViewContext } from "@umbraco-cms/backoffice/view";
import { UMB_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import { GOVERNANCE_ENTITY_TYPE, GOVERNANCE_WORKSPACE_ALIAS } from "./constants.js";

export const GOVERNANCE_WORKSPACE_CONTEXT = new UmbContextToken<MediaGovernanceWorkspaceContext>("UmbMediaGovernanceWorkspaceContext");

/** The "Governance" page in the Media section. Like the Log Viewer, it is one workspace whose views are the header tabs. */
export class MediaGovernanceWorkspaceContext extends UmbContextBase {
    // The workspace-alias condition on each tab reads this.
    workspaceAlias = GOVERNANCE_WORKSPACE_ALIAS;
    // The tabs (workspace views) attach to this view context.
    view = new UmbViewContext(this, null);

    constructor(host: UmbControllerHost) {
        super(host, GOVERNANCE_WORKSPACE_CONTEXT);
        // Workspace conditions and the header's tab bar look for the workspace context.
        this.provideContext(UMB_WORKSPACE_CONTEXT, this as never);
    }

    getEntityType() {
        return GOVERNANCE_ENTITY_TYPE;
    }
}

export { MediaGovernanceWorkspaceContext as api };
