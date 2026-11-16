import { PERSON_MODAL_ALIAS } from "./dashboard/person-modal.token.js";
import { GOVERNANCE_ENTITY_TYPE, GOVERNANCE_WORKSPACE_ALIAS } from "./workspace/constants.js";
const ID = "Umbraco.Community.MediaGovernance";

const FLAG_PREFIX = `${ID}.`;
const signs: Array<{ status: string; label: string; icon: string; color: string }> = [
    // Colours are Umbraco colour names or a CSS colour. "danger" is not an Umbraco colour name, and the pale
    // "yellow" is hard to read at sign size, so warnings use a darker amber.
    { status: "Expired", label: "Expired", icon: "icon-block", color: "red" },
    { status: "NoConsent", label: "No consent", icon: "icon-alert", color: "red" },
    { status: "NeedsTagging", label: "Needs tagging", icon: "icon-tag", color: "#b45309" },
    { status: "ExpiringSoon", label: "Expiring soon", icon: "icon-time", color: "#b45309" },
    // shown on folders: something inside needs attention
    { status: "FolderWithheld", label: "Contains images that cannot be used", icon: "icon-alert", color: "red" },
    { status: "FolderWarning", label: "Contains images that need attention", icon: "icon-info", color: "#b45309" },
];

export const manifests: Array<UmbExtensionManifest> = [
    {
        // The Governance page: one workspace (like the Log Viewer) whose views are the header tabs.
        type: "workspace",
        alias: GOVERNANCE_WORKSPACE_ALIAS,
        name: "Governance Workspace",
        element: () => import("./workspace/governance-workspace.element.js"),
        api: () => import("./workspace/governance-workspace.context.js"),
        meta: { entityType: GOVERNANCE_ENTITY_TYPE },
    },
    ...(
        [
            { key: "Overview", label: "Overview", path: "overview", icon: "icon-chart", weight: 400, element: () => import("./dashboard/overview-tab.element.js") },
            { key: "People", label: "People", path: "people", icon: "icon-users", weight: 300, element: () => import("./dashboard/people-tab.element.js") },
            { key: "Pages", label: "Pages affected", path: "pages", icon: "icon-document", weight: 200, element: () => import("./dashboard/pages-tab.element.js") },
            { key: "Settings", label: "Settings", path: "settings", icon: "icon-settings", weight: 100, element: () => import("./dashboard/settings-tab.element.js") },
        ] as const
    ).map(
        (v): UmbExtensionManifest => ({
            type: "workspaceView",
            alias: `${ID}.WorkspaceView.${v.key}`,
            name: `Governance ${v.label} View`,
            element: v.element,
            weight: v.weight,
            meta: { label: v.label, pathname: v.path, icon: v.icon },
            conditions: [{ alias: "Umb.Condition.WorkspaceAlias", match: GOVERNANCE_WORKSPACE_ALIAS }],
        })
    ),
    {
        type: "modal",
        alias: PERSON_MODAL_ALIAS,
        name: "Governance Person Modal",
        element: () => import("./dashboard/person-modal.element.js"),
    },
    {
        // "Governance" in the Media sidebar (above the tree). Opens the Governance page.
        type: "menuItem",
        alias: `${ID}.MenuItem`,
        name: "Governance Menu Item",
        weight: 200,
        meta: {
            label: "Governance",
            icon: "icon-shield",
            entityType: GOVERNANCE_ENTITY_TYPE,
            menus: ["Umb.Menu.Media"],
        },
        conditions: [{ alias: "Umb.Condition.SectionAlias", match: "Umb.Section.Media" }],
    },
    {
        // Umbraco's media card in the grid view shows no signs, so ours wraps it and adds them
        type: "entityCollectionItemCard",
        alias: `${ID}.MediaCard`,
        name: "Media Governance Media Card",
        element: () => import("./collection/governance-media-card.element.js"),
        weight: 100,
        forEntityTypes: ["media"],
    },
    {
        type: "workspaceView",
        alias: `${ID}.ImageWorkspaceView`,
        name: "Media Governance Image View",
        element: () => import("./workspace-views/image-governance.element.js"),
        weight: 50,
        meta: { label: "Governance", pathname: "governance", icon: "icon-shield" },
        conditions: [
            { alias: "Umb.Condition.WorkspaceAlias", match: "Umb.Workspace.Media" },
            { alias: "Umb.Condition.WorkspaceContentTypeAlias", match: "Image" },
        ],
    },
    ...signs.map(
        (s): UmbExtensionManifest => ({
            type: "entitySign",
            kind: "icon",
            // our own element: the same look as the icon sign, but neutral while its tree row is selected
            element: () => import("./signs/governance-sign.element.js"),
            alias: `${ID}.EntitySign.${s.status}`,
            name: `Media Governance ${s.label} sign`,
            forEntityTypes: ["media"],
            forEntityFlags: [FLAG_PREFIX + s.status],
            weight: 100,
            meta: { iconName: s.icon, label: s.label, iconColorAlias: s.color },
        })
    ),
];
