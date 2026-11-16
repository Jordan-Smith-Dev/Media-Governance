import { css, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

/** The header strip (title on the left, view tabs on the right) is Umbraco's own, so it matches the Log Viewer. */
@customElement("umb-media-governance-workspace")
export class MediaGovernanceWorkspaceElement extends UmbLitElement {
    override render() {
        return html`<umb-workspace-editor headline="Governance" .enforceNoFooter=${true}></umb-workspace-editor>`;
    }

    static override styles = css`
        :host {
            display: block;
            width: 100%;
            height: 100%;
        }
    `;
}

export default MediaGovernanceWorkspaceElement;
export { MediaGovernanceWorkspaceElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-workspace": MediaGovernanceWorkspaceElement;
    }
}
