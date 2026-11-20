import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import { GovernanceApi } from "./index.js";

export const errorMessage = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Base class giving an element the package API and a notify helper. */
export abstract class GovernanceElement extends UmbLitElement {
  #notifications?: UmbNotificationContext;

  protected api = new GovernanceApi();

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this.#notifications = ctx));
  }

  protected notify(color: "positive" | "danger", message: string) {
    this.#notifications?.peek(color, { data: { message } });
  }
}
