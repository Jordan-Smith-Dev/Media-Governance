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
        type: "dashboard",
        alias: `${ID}.Dashboard`,
        name: "Media Governance Dashboard",
        element: () => import("./dashboard/dashboard.element.js"),
        weight: 100,
        meta: { label: "Media governance", pathname: "media-governance" },
        conditions: [{ alias: "Umb.Condition.SectionAlias", match: "Umb.Section.Media" }],
    },
    {
        // A shortcut in the Media sidebar (above the tree) so the dashboard is one click away from any media item.
        type: "menuItem",
        kind: "link",
        alias: `${ID}.MenuItem`,
        name: "Media Governance Menu Item",
        weight: 200,
        meta: {
            label: "Media governance",
            icon: "icon-shield",
            href: "/umbraco/section/media/dashboard/media-governance",
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
