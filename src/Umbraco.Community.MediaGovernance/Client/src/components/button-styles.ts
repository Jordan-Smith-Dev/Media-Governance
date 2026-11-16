import { css } from "@umbraco-cms/backoffice/external/lit";

/**
 * One width for every action button in the package, so rows and footers line up.
 * Opt in with class="action". Icon-only buttons, tag chips and the date picker are left to size themselves.
 */
export const buttonStyles = css`
    uui-button.action {
        min-width: 9rem;
    }
    uui-button.action[compact] {
        min-width: 6.5rem;
    }
`;
