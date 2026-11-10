import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";

const API_BASE = "/umbraco/umbracocommunitymediagovernance/api/v1";

export type GovernanceStatus = "Compliant" | "ExpiringSoon" | "Expired" | "NoConsent" | "NeedsTagging";

export const STATUS_LABELS: Record<GovernanceStatus, string> = {
    Compliant: "Compliant",
    ExpiringSoon: "Expiring soon",
    Expired: "Expired",
    NoConsent: "No consent",
    NeedsTagging: "Needs tagging",
};

export type ExpirySource = "None" | "Default" | "Custom" | "Consent";

export type OverviewItem = {
    mediaKey: string;
    name: string;
    folder: string;
    people: string[];
    dateTaken: string | null;
    expiry: string | null;
    daysToExpiry: number | null;
    consent: string;
    status: GovernanceStatus;
    expirySource: ExpirySource;
    issues: GovernanceStatus[];
    noPeople: boolean;
};

export type OverviewPage = {
    counts: Record<GovernanceStatus, number>;
    allCount: number;
    items: OverviewItem[];
    total: number;
};

export type Person = {
    id: string;
    name: string;
    email: string | null;
    phone: string | null;
    linkedMemberKey: string | null;
    linkedUserKey: string | null;
    jobTitle: string | null;
    taggedInCount: number;
};

export type PersonRequest = Omit<Person, "id" | "taggedInCount">;

export type Consent = {
    id: string;
    personId: string;
    givenOn: string;
    givenBy: string;
    permittedUses: string[];
    expiresOn: string | null;
    withdrawnOn: string | null;
    hasEvidence: boolean;
    notes: string | null;
};

export type ConsentRequest = {
    personId: string;
    givenOn: string;
    givenBy: string;
    permittedUses: string[];
    expiresOn: string | null;
    notes: string | null;
};

export type ImageGovernance = {
    mediaKey: string;
    dateTaken: string | null;
    dateObtained: string | null;
    customExpiry: string | null;
    noPeople: boolean;
    taggedPersonIds: string[];
    effectiveExpiry: string | null;
    expirySource: ExpirySource;
    status: GovernanceStatus;
    issues: GovernanceStatus[];
    clearedUses: string[];
    consents: Consent[];
    dateUploaded: string | null;
};

export type ImageGovernanceRequest = Pick<
    ImageGovernance,
    "dateTaken" | "dateObtained" | "customExpiry" | "noPeople" | "taggedPersonIds"
>;

export type AffectedImage = { mediaKey: string; name: string; status: GovernanceStatus };

export type AffectedPage = {
    pageKey: string;
    name: string;
    published: boolean;
    contentType: string | null;
    contentTypeIcon: string | null;
    images: AffectedImage[];
};

export type Settings = {
    retentionYears: number;
    warningDays: number;
    permittedUses: string[];
    notificationEmails: string[];
};

export class ApiError extends Error {
    constructor(public status: number, message: string) {
        super(message);
    }
}

/**
 * Talks to this package's Management API controllers through Umbraco's own HTTP client, the same way Umbraco's
 * generated SDK calls do. That client already handles sign-in (cookie or token), token refresh and 401 retries,
 * so nothing here deals with tokens.
 */
export class GovernanceApi {
    async #request(
        method: "GET" | "POST" | "PUT" | "DELETE",
        path: string,
        options: { body?: unknown; form?: FormData; parseAs?: "json" | "blob" | "text" } = {}
    ): Promise<unknown> {
        const result = (await (umbHttpClient as any).request({
            method,
            url: `${API_BASE}/${path}`,
            // Declaring a bearer scheme is what makes the client attach the backoffice credentials.
            security: [{ type: "http", scheme: "bearer" }],
            throwOnError: false,
            parseAs: options.parseAs ?? "auto",
            ...(options.form
                ? { body: options.form, bodySerializer: null, headers: { "Content-Type": null } }
                : options.body !== undefined
                  ? { body: options.body, headers: { "Content-Type": "application/json" } }
                  : {}),
        })) as { data?: unknown; error?: unknown; response?: Response };

        const response = result.response;
        if (!response || !response.ok) {
            const err = result.error;
            const message =
                typeof err === "string" && err
                    ? err
                    : ((err as { detail?: string; title?: string } | undefined)?.detail ??
                      (err as { title?: string } | undefined)?.title ??
                      `Request failed (${response?.status ?? "no response"})`);
            throw new ApiError(response?.status ?? 0, message);
        }
        return result.data;
    }

    async #json<T>(method: "GET" | "POST" | "PUT", path: string, body?: unknown): Promise<T> {
        return (await this.#request(method, path, { body })) as T;
    }

    overview(status: GovernanceStatus | null, filter: string, skip: number, take: number) {
        const q = new URLSearchParams({ skip: String(skip), take: String(take) });
        if (status) q.set("status", status);
        if (filter) q.set("filter", filter);
        return this.#json<OverviewPage>("GET", `overview?${q}`);
    }

    async exportCsv(status: GovernanceStatus | null, filter: string, keys?: string[]): Promise<void> {
        const q = new URLSearchParams();
        if (status) q.set("status", status);
        if (filter) q.set("filter", filter);
        const blob = (await this.#request("GET", `overview/export?${q}${keys?.length ? `&keys=${keys.join(",")}` : ""}`, { parseAs: "blob" })) as Blob;
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = "media-governance.csv";
        a.click();
        URL.revokeObjectURL(url);
    }

    people(query = "") {
        return this.#json<Person[]>("GET", `people?q=${encodeURIComponent(query)}`);
    }
    createPerson(p: PersonRequest) {
        return this.#json<Person>("POST", "people", p);
    }
    updatePerson(id: string, p: PersonRequest) {
        return this.#json<Person>("PUT", `people/${id}`, p);
    }
    async erasePerson(id: string): Promise<void> {
        await this.#request("DELETE", `people/${id}`);
    }

    image(mediaKey: string) {
        return this.#json<ImageGovernance>("GET", `image/${mediaKey}`);
    }
    saveImage(mediaKey: string, r: ImageGovernanceRequest) {
        return this.#json<ImageGovernance>("PUT", `image/${mediaKey}`, r);
    }
    addConsent(mediaKey: string, r: ConsentRequest) {
        return this.#json<Consent>("POST", `image/${mediaKey}/consent`, r);
    }
    async withdrawConsent(consentId: string, on: string | null): Promise<void> {
        await this.#request("POST", `consent/${consentId}/withdraw`, { body: { on } });
    }
    /** Fetches a consent's evidence with the backoffice credentials. Each call is audited as a view. */
    async evidence(consentId: string): Promise<Blob> {
        return (await this.#request("GET", `consent/${consentId}/evidence`, { parseAs: "blob" })) as Blob;
    }
    async uploadEvidence(consentId: string, file: File): Promise<void> {
        const form = new FormData();
        form.append("file", file);
        await this.#request("POST", `consent/${consentId}/evidence`, { form });
    }

    pagesAffected() {
        return this.#json<AffectedPage[]>("GET", "pages-affected");
    }

    settings() {
        return this.#json<Settings>("GET", "settings");
    }
    saveSettings(s: Settings) {
        return this.#json<Settings>("PUT", "settings", s);
    }
}
