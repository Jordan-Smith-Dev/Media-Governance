import { buttonStyles } from "../components/button-styles.js";
import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UMB_MEDIA_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/media";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import "../components/date-input.element.js";
import { GovernanceElement, errorMessage } from "../api/governance-element.js";
import { STATUS_HELP, STATUS_LABELS, type Consent, type EvidenceEvent, type ImageGovernance, type Person, type Settings } from "../api/index.js";
import { GIVEN_BY_OPTIONS, STATUS_COLORS, daysUntil, formatBytes, formatDate, givenByLabel, relativeTime, todayIso } from "../dashboard/format.js";

type ConsentForm = {
    personId: string;
    givenOn: string;
    givenBy: string;
    uses: Set<string>;
    expiresOn: string;
    notes: string;
    file: File | null;
};

const MAX_SUGGESTIONS = 8;

const blankConsent = (personId = ""): ConsentForm => ({
    personId,
    givenOn: todayIso(),
    givenBy: "Self",
    uses: new Set(),
    expiresOn: "",
    notes: "",
    file: null,
});

@customElement("umb-media-governance-image-view")
export class MediaGovernanceImageViewElement extends GovernanceElement {
    @state() private _mediaKey = "";
    @state() private _data?: ImageGovernance;
    @state() private _people: Person[] = [];
    @state() private _settings?: Settings;
    @state() private _error = "";
    @state() private _consentForm: ConsentForm | null = null;
    @state() private _saveState: "idle" | "saving" | "saved" = "idle";
    @state() private _preview: { url: string; type: string; name: string; broken: boolean } | null = null;
    @state() private _history: { person: string; events: EvidenceEvent[] } | null = null;
    #pending: Promise<unknown> = Promise.resolve();
    #savedTimer?: number;
    @state() private _personSearch = "";

    constructor() {
        super();
        this.consumeContext(UMB_MEDIA_WORKSPACE_CONTEXT, (ctx) => {
            this.observe(ctx?.unique, (unique) => {
                if (unique && unique !== this._mediaKey) {
                    this._mediaKey = unique;
                    void this.#load();
                }
            });
        });
    }

    async #load() {
        this._error = "";
        try {
            const [data, people, settings] = await Promise.all([
                this.api.image(this._mediaKey),
                this.api.people(),
                this.api.settings(),
            ]);
            this._data = data;
            this._people = people;
            this._settings = settings;
        } catch (e) {
            this._error = errorMessage(e);
        }
    }

    /** Applies a change and saves it immediately, so the only Save button in the UI is Umbraco's own. */
    #patch(change: Partial<ImageGovernance>) {
        if (!this._data) return;
        this._data = { ...this._data, ...change };
        void this.#persist();
    }

    #date(field: "dateTaken" | "dateObtained" | "customExpiry", e: Event) {
        const value = (e as CustomEvent<string>).detail;
        this.#patch({ [field]: value || null });
    }

    #addPerson(id: string) {
        if (!this._data || this._data.taggedPersonIds.includes(id)) return;
        this.#patch({ taggedPersonIds: [...this._data.taggedPersonIds, id], noPeople: false });
        this._personSearch = "";
    }

    #removePerson(id: string) {
        if (!this._data) return;
        this.#patch({ taggedPersonIds: this._data.taggedPersonIds.filter((p) => p !== id) });
    }

    /** Saves the current details. Saves are queued so they reach the server in order. */
    #persist(): Promise<unknown> {
        this._saveState = "saving";
        this.#pending = this.#pending.then(async () => {
            const d = this._data;
            if (!d) return;
            try {
                const saved = await this.api.saveImage(this._mediaKey, {
                    dateTaken: d.dateTaken,
                    dateObtained: d.dateObtained,
                    customExpiry: d.customExpiry,
                    noPeople: d.noPeople,
                    taggedPersonIds: d.taggedPersonIds,
                });
                // Keep the status and expiry fresh; keep what the user has typed since this save started.
                this._data = { ...saved, ...this.#latestEdits(saved) };
                this._saveState = "saved";
                window.clearTimeout(this.#savedTimer);
                this.#savedTimer = window.setTimeout(() => (this._saveState = "idle"), 2500);
            } catch (e) {
                this.notify("danger", `Not saved: ${errorMessage(e)}`);
                this._saveState = "idle";
                await this.#load(); // show what the server actually holds
            }
        });
        return this.#pending;
    }

    #latestEdits(saved: ImageGovernance): Partial<ImageGovernance> {
        const current = this._data;
        return current ? { dateTaken: current.dateTaken, dateObtained: current.dateObtained, customExpiry: current.customExpiry, noPeople: current.noPeople, taggedPersonIds: current.taggedPersonIds, consents: saved.consents } : {};
    }

    /** Opens the consent form once any tag changes have reached the server (consent needs saved tags). */
    async #openConsentForm() {
        await this.#pending;
        const tagged = this._data?.taggedPersonIds ?? [];
        this._consentForm = blankConsent(tagged.length === 1 ? tagged[0] : "");
        await this.updateComplete;
        this.shadowRoot?.querySelector(".consent-form")?.scrollIntoView({ behavior: "smooth", block: "center" });
    }

    async #addConsent() {
        const f = this._consentForm;
        if (!f) return;
        if (!f.personId) {
            this.notify("danger", "Choose the person who is giving consent.");
            return;
        }
        if (f.uses.size === 0) {
            this.notify("danger", "Choose at least one permitted use.");
            return;
        }
        try {
            await this.#pending;
            const consent = await this.api.addConsent(this._mediaKey, {
                personId: f.personId,
                givenOn: f.givenOn,
                givenBy: f.givenBy,
                permittedUses: [...f.uses],
                expiresOn: f.expiresOn || null,
                notes: f.notes.trim() || null,
            });
            this._consentForm = null; // the consent exists now: never leave the form open to be submitted twice
            this.notify("positive", "Consent recorded");
            if (f.file) {
                try {
                    await this.api.uploadEvidence(consent.id, f.file);
                } catch (e) {
                    this.notify("danger", `Consent recorded, but the evidence was not uploaded: ${errorMessage(e)}`);
                }
            }
            await this.#load();
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    async #withdraw(c: Consent) {
        try {
            await umbConfirmModal(this, {
                headline: `Withdraw consent from ${this.#personName(c.personId)}?`,
                content: "The image will show as having no consent for this person until new consent is recorded. The record is kept for the audit history.",
                color: "danger",
                confirmLabel: "Withdraw consent",
            });
        } catch {
            return; // cancelled
        }
        try {
            await this.api.withdrawConsent(c.id, null);
            this.notify("positive", "Consent withdrawn");
            await this.#load();
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    async #removeEvidence(c: Consent) {
        try {
            await umbConfirmModal(this, {
                headline: `Remove the evidence for ${this.#personName(c.personId)}?`,
                content: "The file is deleted and cannot be recovered. The consent record stays, and the removal is recorded in the audit log.",
                color: "danger",
                confirmLabel: "Remove evidence",
            });
        } catch {
            return; // cancelled
        }
        try {
            await this.api.removeEvidence(c.id);
            this.notify("positive", "Evidence removed");
            await this.#load();
        } catch (e) {
            this.notify("danger", errorMessage(e));
        }
    }

    #evidenceTarget?: Consent;

    #pickEvidence(c: Consent) {
        this.#evidenceTarget = c;
        (this.shadowRoot?.querySelector("#evidence-input") as HTMLInputElement | null)?.click();
    }

    async #viewEvidence(c: Consent) {
        try {
            const blob = await this.api.evidence(c.id);
            this.#closePreview();
            // A PDF that is not really a PDF shows as an empty frame, so check before trying to show it.
            const broken = blob.type === "application/pdf" && (await blob.slice(0, 5).text()) !== "%PDF-";
            this._preview = {
                broken,
                url: URL.createObjectURL(blob),
                type: blob.type,
                name: `consent-evidence-${c.id}.${blob.type === "application/pdf" ? "pdf" : blob.type === "image/png" ? "png" : "jpg"}`,
            };
        } catch (e) {
            this.notify("danger", `Evidence could not be opened: ${errorMessage(e)}`);
        }
    }

    // uui-button ignores the download attribute, so click a real anchor instead (as the CSV export does).
    #downloadPreview = () => {
        const p = this._preview;
        if (!p) return;
        const a = document.createElement("a");
        a.href = p.url;
        a.download = p.name;
        a.click();
    };

    #closePreview = () => {
        if (this._preview) URL.revokeObjectURL(this._preview.url);
        this._preview = null;
    };

    override updated() {
        // Open dialogs once they have been rendered (a native dialog must be opened by script).
        this.shadowRoot?.querySelectorAll<HTMLDialogElement>("dialog[data-modal]").forEach((d) => {
            if (!d.open) d.showModal();
        });
    }

    async #showHistory(c: Consent) {
        try {
            this._history = { person: this.#personName(c.personId), events: await this.api.evidenceHistory(c.id) };
        } catch (e) {
            this.notify("danger", `History could not be loaded: ${errorMessage(e)}`);
        }
    }

    #closeHistory = () => {
        this._history = null;
    };

    #renderHistory() {
        const h = this._history;
        if (!h) return nothing;
        const label: Record<string, string> = {
            "consent.evidence": "Uploaded",
            "consent.evidence.viewed": "Viewed",
            "consent.evidence.removed": "Removed",
        };
        return html`
            <dialog class="preview history" data-modal aria-labelledby="history-title" @close=${this.#closeHistory} @click=${(e: Event) => e.target === e.currentTarget && this.#closeHistory()}>
                <div class="preview-head">
                    <strong id="history-title">Evidence history for ${h.person}</strong>
                    <uui-button compact look="secondary" label="Close" @click=${this.#closeHistory}><uui-icon name="icon-wrong"></uui-icon></uui-button>
                </div>
                <div class="history-body">
                    ${h.events.length === 0
                        ? html`<p class="muted">Nothing has happened to this evidence yet.</p>`
                        : html`<uui-table>
                              <uui-table-head>
                                  <uui-table-head-cell>What</uui-table-head-cell>
                                  <uui-table-head-cell>Who</uui-table-head-cell>
                                  <uui-table-head-cell>When</uui-table-head-cell>
                              </uui-table-head>
                              ${h.events.map(
                                  (e) => html`<uui-table-row>
                                      <uui-table-cell>${label[e.action] ?? e.action}</uui-table-cell>
                                      <uui-table-cell>${e.actor}</uui-table-cell>
                                      <uui-table-cell>${new Date(e.whenUtc).toLocaleString()}</uui-table-cell>
                                  </uui-table-row>`
                              )}
                          </uui-table>`}
                </div>
                <div class="preview-foot">
                    <span class="muted">Covers this person on this image. Viewing is recorded each time.</span>
                    <uui-button class="action" look="secondary" label="Close" @click=${this.#closeHistory}>Close</uui-button>
                </div>
            </dialog>
        `;
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        this.#closePreview();
    }

    #renderPreview() {
        const p = this._preview;
        if (!p) return nothing;
        return html`
            <dialog class="preview" data-modal aria-labelledby="preview-title" @close=${this.#closePreview} @click=${(e: Event) => e.target === e.currentTarget && this.#closePreview()}>
                <div class="preview-head">
                    <strong id="preview-title">Consent evidence</strong>
                    <uui-button compact look="secondary" label="Close" @click=${this.#closePreview}><uui-icon name="icon-wrong"></uui-icon></uui-button>
                </div>
                <div class="preview-body">
                    ${p.broken
                        ? html`<p class="preview-broken">This file could not be previewed. It may be damaged. Use Download to check it, or upload a replacement.</p>`
                        : p.type === "application/pdf"
                          ? html`<iframe src=${p.url} title="Consent evidence (PDF)"></iframe>`
                          : html`<img src=${p.url} alt="Consent evidence" @error=${() => (this._preview = { ...p, broken: true })} />`}
                </div>
                <div class="preview-foot">
                    <span class="muted">Opening or downloading this file is recorded in the audit log.</span>
                    <uui-button class="action" look="primary" label="Download" @click=${this.#downloadPreview}>
                        <uui-icon name="icon-download-alt"></uui-icon> Download
                    </uui-button>
                </div>
            </dialog>
        `;
    }

    async #uploadEvidence(e: Event) {
        const input = e.target as HTMLInputElement;
        const file = input.files?.[0];
        const consent = this.#evidenceTarget;
        input.value = ""; // allow picking the same file again
        if (!file || !consent) return;
        try {
            await this.api.uploadEvidence(consent.id, file);
            this.notify("positive", "Evidence uploaded");
            await this.#load();
        } catch (err) {
            this.notify("danger", errorMessage(err));
        }
    }

    /** Status of a consent record for display: withdrawn, expired, or active (with its expiry). */
    #consentStatus(c: Consent): { label: string; color: "positive" | "danger" | "warning"; help: string } {
        if (c.withdrawnOn) return { label: `Withdrawn ${formatDate(c.withdrawnOn)}`, color: "danger", help: "This person withdrew consent. The record is kept for the audit history." };
        if (c.expiresOn && c.expiresOn <= todayIso()) return { label: `Expired ${formatDate(c.expiresOn)}`, color: "danger", help: "The consent has passed its end date and no longer covers use of the image." };
        return { label: c.expiresOn ? `Active until ${formatDate(c.expiresOn)}` : "Active, no expiry", color: "positive", help: "Consent is in force for the permitted uses shown." };
    }

    #personName(id: string) {
        return this._people.find((p) => p.id === id)?.name ?? "Unknown person";
    }

    override render() {
        if (this._error) return html`<div class="wrap"><uui-tag color="danger">${this._error}</uui-tag></div>`;
        const d = this._data;
        if (!d) return html`<div class="wrap"><uui-loader></uui-loader></div>`;

        return html`
            <div class="wrap">
                <uui-box headline="Compliance">
                    <div class="status">
                        <div class="pills">
                            ${d.issues.length === 0
                                ? html`<uui-tag color=${STATUS_COLORS.Compliant}>${STATUS_LABELS.Compliant}</uui-tag>`
                                : d.issues.map((s) => html`<uui-tag color=${STATUS_COLORS[s]} title=${STATUS_HELP[s]}>${STATUS_LABELS[s]}</uui-tag>`)}
                        </div>
                        <span class="save-state">
                            ${this._saveState === "saving" ? "Saving..." : this._saveState === "saved" ? "Saved" : ""}
                        </span>
                    </div>
                    <div class="expiry-line">${this.#renderExpiry(d)}</div>
                    ${this.#renderCleared(d)}
                </uui-box>

                <uui-box headline="Image details">
                    <umb-property-layout label="Date taken" description=${this.#dateTakenHint(d)}>
                        <umb-media-governance-date-input slot="editor" label="Date taken" .value=${d.dateTaken ?? ""} @change=${(e: Event) => this.#date("dateTaken", e)}></umb-media-governance-date-input>
                    </umb-property-layout>
                    <umb-property-layout label="Date obtained" description="Used when the date taken is not known.">
                        <umb-media-governance-date-input slot="editor" label="Date obtained" .value=${d.dateObtained ?? ""} @change=${(e: Event) => this.#date("dateObtained", e)}></umb-media-governance-date-input>
                    </umb-property-layout>
                    <umb-property-layout label="Custom expiry" description="Overrides the default retention period.">
                        <umb-media-governance-date-input slot="editor" label="Custom expiry" .value=${d.customExpiry ?? ""} @change=${(e: Event) => this.#date("customExpiry", e)}></umb-media-governance-date-input>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="People pictured">
                    <umb-property-layout label="Contains no people" description="Tick when nobody is pictured, so the image does not need tagging.">
                        <uui-toggle slot="editor" label="Contains no people" .checked=${d.noPeople} ?disabled=${d.taggedPersonIds.length > 0} @change=${(e: Event) => this.#patch({ noPeople: (e.target as HTMLInputElement).checked })}></uui-toggle>
                    </umb-property-layout>
                    <umb-property-layout label="Tagged people" description="Everyone recognisable in the image needs their own consent.">
                        <div slot="editor" class="people">${this.#renderPeoplePicker(d)}</div>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="Consent records">${this.#renderConsents(d)}</uui-box>

                <p class="autosave-note">
                    Governance details save as you change them. The Save button below saves the media item itself.
                </p>
            </div>
        `;
    }

    #dateTakenHint(d: ImageGovernance): string {
        if (d.dateTaken) return "Retention is counted from this date.";
        if (d.dateObtained) return "Not set: the date obtained is used instead.";
        if (d.dateUploaded) return `Not set: the upload date (${formatDate(d.dateUploaded)}) is used instead.`;
        return "Retention is counted from this date.";
    }

    /** "Expires 15 January 2028 · in 15 months · default (3 years after the date taken)". */
    #renderExpiry(d: ImageGovernance) {
        if (!d.effectiveExpiry) return html`<span class="muted">Image expiry: none yet. Add a date taken or obtained.</span>`;
        const days = daysUntil(d.effectiveExpiry);
        const source =
            d.expirySource === "Custom"
                ? "custom expiry"
                : d.expirySource === "Consent"
                  ? "ends with a person's consent"
                  : `default (${this._settings?.retentionYears ?? 3} years after the ${d.dateTaken ? "date taken" : d.dateObtained ? "date obtained" : d.dateUploaded ? "upload date" : "date taken"})`;
        return html`
            <span>
                Image ${days < 0 ? "expired" : "can be used until"} <strong>${formatDate(d.effectiveExpiry)}</strong>
                <span class="muted">· ${relativeTime(days)} · ${source}</span>
            </span>
        `;
    }

    /** Which uses (Web, Print, Marketing...) every tagged person has consented to. */
    #renderCleared(d: ImageGovernance) {
        const uses = this._settings?.permittedUses ?? [];
        if (uses.length === 0) return nothing;
        return html`
            <div class="cleared">
                ${uses.map((u) => {
                    const ok = d.clearedUses.some((c) => c.toLowerCase() === u.toLowerCase());
                    return html`<uui-tag
                        look="primary"
                        color=${ok ? "positive" : "danger"}
                        title=${ok ? `Cleared for ${u} use: every tagged person has consented` : `Not cleared for ${u} use: someone tagged has not consented to it`}>
                        ${ok ? "✓" : "✕"} ${u}
                    </uui-tag>`;
                })}
            </div>
        `;
    }

    #renderPeoplePicker(d: ImageGovernance) {
        const available = this._people.filter((p) => !d.taggedPersonIds.includes(p.id));
        const term = this._personSearch.trim().toLowerCase();
        const matches = available.filter((p) => !term || p.name.toLowerCase().includes(term));

        return html`
            <div class="tags">
                ${d.taggedPersonIds.length === 0 ? html`<span class="muted">No one tagged yet.</span>` : nothing}
                ${d.taggedPersonIds.map(
                    (id) => html`
                        <uui-tag look="secondary">
                            ${this.#personName(id)}
                            <uui-button compact label="Remove ${this.#personName(id)}" @click=${() => this.#removePerson(id)}>
                                <uui-icon name="icon-wrong"></uui-icon>
                            </uui-button>
                        </uui-tag>
                    `
                )}
            </div>

            ${available.length === 0
                ? html`<span class="muted">${this._people.length === 0 ? "No people exist yet. Add them in the Media governance dashboard, People tab." : "Everyone is already tagged."}</span>`
                : html`
                      <uui-input label="Search people to tag" placeholder="Search people to tag..." .value=${this._personSearch} @input=${(e: Event) => (this._personSearch = (e.target as HTMLInputElement).value)}></uui-input>
                      <div class="suggestions">
                          ${matches.slice(0, MAX_SUGGESTIONS).map(
                              (p) => html`<uui-button look="outline" compact label="Tag ${p.name}" @click=${() => this.#addPerson(p.id)}>
                                  <uui-icon name="icon-add"></uui-icon> ${p.name}
                              </uui-button>`
                          )}
                          ${matches.length === 0 ? html`<span class="muted">No one matches.</span>` : nothing}
                          ${matches.length > MAX_SUGGESTIONS ? html`<span class="muted">+${matches.length - MAX_SUGGESTIONS} more, keep typing to narrow the list.</span>` : nothing}
                      </div>
                  `}
        `;
    }

    #renderConsents(d: ImageGovernance) {
        const hasTags = d.taggedPersonIds.length > 0;
        return html`
            ${d.consents.length === 0
                ? html`<p class="muted">No consent recorded yet. Tag the people pictured above, then use Add consent to record each person's consent.</p>`
                : html`
                      <uui-table class="consents">
                          <uui-table-head>
                              <uui-table-head-cell>Person</uui-table-head-cell>
                              <uui-table-head-cell>Given</uui-table-head-cell>
                              <uui-table-head-cell>Permitted uses</uui-table-head-cell>
                              <uui-table-head-cell>Status</uui-table-head-cell>
                              <uui-table-head-cell>Evidence</uui-table-head-cell>
                              <uui-table-head-cell></uui-table-head-cell>
                          </uui-table-head>
                          ${d.consents.map((c) => {
                              const status = this.#consentStatus(c);
                              return html`
                                  <uui-table-row>
                                      <uui-table-cell>
                                          <a class="person-link" href="/umbraco/section/media/dashboard/media-governance?person=${c.personId}" title="Open in People">${this.#personName(c.personId)}</a>
                                      </uui-table-cell>
                                      <uui-table-cell>
                                          ${formatDate(c.givenOn)}
                                          <div class="muted">${givenByLabel(c.givenBy)}</div>
                                      </uui-table-cell>
                                      <uui-table-cell>
                                          <div class="chips">
                                              ${c.permittedUses.length ? c.permittedUses.map((u) => html`<uui-tag look="primary" color="positive">${u}</uui-tag>`) : "—"}
                                          </div>
                                      </uui-table-cell>
                                      <uui-table-cell title=${status.help}>${status.label}</uui-table-cell>
                                      <uui-table-cell>
                                          <div class="evidence-cell">
                                          <div class="action-buttons evidence-buttons">
                                              ${c.hasEvidence
                                                  ? html`<uui-button class="action" look="primary" compact label="View evidence" @click=${() => this.#viewEvidence(c)}>View</uui-button>`
                                                  : nothing}
                                              <uui-button class="action" look="secondary" compact label=${c.hasEvidence ? "Replace evidence" : "Upload evidence"} @click=${() => this.#pickEvidence(c)}>
                                                  ${c.hasEvidence ? "Replace" : "Upload"}
                                              </uui-button>
                                              ${c.hasEvidence
                                                  ? html`<uui-button class="action" look="secondary" color="danger" compact label="Remove evidence" @click=${() => this.#removeEvidence(c)}>Remove</uui-button>`
                                                  : nothing}
                                          </div>
                                          ${c.hasEvidence
                                              ? html`<div class="evidence-meta muted">
                                                    ${[c.evidenceType, c.evidenceSizeBytes != null ? formatBytes(c.evidenceSizeBytes) : "file missing"].filter(Boolean).join(" · ")}
                                                    · <button class="link" @click=${() => this.#showHistory(c)}>History</button>
                                                </div>`
                                              : nothing}
                                          </div>
                                      </uui-table-cell>
                                      <uui-table-cell class="actions">
                                          ${c.withdrawnOn
                                              ? nothing
                                              : html`<uui-button class="action" look="primary" color="danger" compact label="Withdraw consent" @click=${() => this.#withdraw(c)}>Withdraw</uui-button>`}
                                      </uui-table-cell>
                                  </uui-table-row>
                              `;
                          })}
                      </uui-table>
                      <input id="evidence-input" type="file" hidden accept=".pdf,.png,.jpg,.jpeg" @change=${this.#uploadEvidence} />
                  `}
            ${this._consentForm
                ? this.#renderConsentForm(this._consentForm, d.taggedPersonIds)
                : html`
                      <div class="add-consent">
                          <uui-button class="action" look="primary" label="Add consent" ?disabled=${!hasTags} @click=${this.#openConsentForm}>
                              <uui-icon name="icon-add"></uui-icon> Add consent
                          </uui-button>
                          ${hasTags ? nothing : html`<span class="muted">Tag at least one person above to record their consent.</span>`}
                      </div>
                  `}
            ${this.#renderPreview()} ${this.#renderHistory()}
        `;
    }

    #renderConsentForm(f: ConsentForm, personIds: string[]) {
        const set = (change: Partial<ConsentForm>) => (this._consentForm = { ...f, ...change });
        const options = [
            { name: "Choose a person...", value: "", selected: f.personId === "" },
            ...personIds.map((id) => ({ name: this.#personName(id), value: id, selected: id === f.personId })),
        ];
        return html`
            <div class="consent-form">
                <h4>Record consent</h4>
                <umb-property-layout label="Person" description="Who is giving consent.">
                    <uui-select slot="editor" label="Person" .options=${options} @change=${(e: Event) => set({ personId: (e.target as HTMLSelectElement).value })}></uui-select>
                </umb-property-layout>
                <umb-property-layout label="Given on">
                    <umb-media-governance-date-input slot="editor" label="Given on" .value=${f.givenOn} @change=${(e: Event) => set({ givenOn: (e as CustomEvent<string>).detail })}></umb-media-governance-date-input>
                </umb-property-layout>
                <umb-property-layout label="Given by" description="Only these people can give a valid consent.">
                    <uui-radio-group slot="editor" class="radios" .value=${f.givenBy} @change=${(e: Event) => set({ givenBy: (e.target as HTMLInputElement).value })}>
                        ${GIVEN_BY_OPTIONS.map((o) => html`<uui-radio value=${o.value} label=${o.label}></uui-radio>`)}
                    </uui-radio-group>
                </umb-property-layout>
                <umb-property-layout label="Permitted uses">
                    <div slot="editor" class="uses">
                        ${(this._settings?.permittedUses ?? []).map(
                            (u) => html`<uui-checkbox label=${u} .checked=${f.uses.has(u)} @change=${(e: Event) => {
                                const uses = new Set(f.uses);
                                (e.target as HTMLInputElement).checked ? uses.add(u) : uses.delete(u);
                                set({ uses });
                            }}></uui-checkbox>`
                        )}
                    </div>
                </umb-property-layout>
                <umb-property-layout label="Expires on" description="Leave empty if the consent does not expire.">
                    <umb-media-governance-date-input slot="editor" label="Expires on" .value=${f.expiresOn} @change=${(e: Event) => set({ expiresOn: (e as CustomEvent<string>).detail })}></umb-media-governance-date-input>
                </umb-property-layout>
                <umb-property-layout label="Notes">
                    <uui-textarea slot="editor" label="Notes" .value=${f.notes} @input=${(e: Event) => set({ notes: (e.target as HTMLTextAreaElement).value })}></uui-textarea>
                </umb-property-layout>
                <umb-property-layout label="Evidence" description="Signed form or email. PDF, PNG or JPG, up to 10 MB.">
                    <input slot="editor" type="file" accept=".pdf,.png,.jpg,.jpeg" @change=${(e: Event) => set({ file: (e.target as HTMLInputElement).files?.[0] ?? null })} />
                </umb-property-layout>
                <div class="form-buttons">
                    <uui-button class="action" look="secondary" label="Cancel" @click=${() => (this._consentForm = null)}>Cancel</uui-button>
                    <uui-button class="action" look="primary" color="positive" label="Record consent" @click=${this.#addConsent}>Record consent</uui-button>
                </div>
            </div>
        `;
    }

    static override styles = [buttonStyles, css`
        :host { display: flex; flex-direction: column; min-height: 100%; }
        .wrap { flex: 1; padding: var(--uui-size-layout-1); display: flex; flex-direction: column; gap: var(--uui-size-layout-1); }
        uui-box { --uui-box-default-padding: var(--uui-size-space-6) var(--uui-size-space-6); }
        umb-property-layout { padding: var(--uui-size-space-4) 0; }
        umb-property-layout:first-of-type { padding-top: 0; }
        umb-property-layout:last-of-type { padding-bottom: 0; }
        .status { display: flex; gap: var(--uui-size-space-5); align-items: center; justify-content: space-between; flex-wrap: wrap; }
        .pills { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); }
        .expiry-line { margin-top: var(--uui-size-space-4); }
        .cleared { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-top: var(--uui-size-space-5); padding-top: var(--uui-size-space-4); border-top: 1px solid var(--uui-color-border); }
        .people { display: flex; flex-direction: column; gap: var(--uui-size-space-4); }
        .tags, .suggestions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
        .radios { display: flex; flex-direction: row; flex-wrap: wrap; gap: var(--uui-size-space-5); }
        .uses { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-5); }
        .muted { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
        .autosave-note { margin: 0; color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); text-align: right; }
        .add-consent { display: flex; align-items: center; gap: var(--uui-size-space-4); margin-top: var(--uui-size-space-5); }
        uui-table + .add-consent { margin-top: var(--uui-size-space-6); }
        .consent-form { margin-top: var(--uui-size-space-6); padding-top: var(--uui-size-space-5); border-top: 1px solid var(--uui-color-border); }
        .consent-form h4 { margin: 0 0 var(--uui-size-space-5); }
        .form-buttons { display: flex; justify-content: flex-end; gap: var(--uui-size-space-3); margin-top: var(--uui-size-space-5); }
        uui-input, uui-select, uui-textarea { max-width: 420px; width: 100%; }
        uui-table-cell { vertical-align: middle; }
        .consents uui-table-cell { padding-top: var(--uui-size-space-4); padding-bottom: var(--uui-size-space-4); }
        .chips { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
        .actions { text-align: right; white-space: nowrap; }
        .action-buttons { display: flex; align-items: center; justify-content: flex-end; gap: var(--uui-size-space-2); }
        .evidence-buttons { justify-content: flex-start; }
        .evidence-cell { display: flex; flex-direction: column; gap: var(--uui-size-space-2); }
        .evidence-meta { white-space: nowrap; }
        button.link { all: unset; cursor: pointer; color: var(--uui-color-interactive); text-decoration: underline; }
        button.link:hover { color: var(--uui-color-interactive-emphasis); }
        button.link:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
        .history-body { padding: var(--uui-size-space-4) var(--uui-size-space-5); max-height: 60vh; overflow: auto; }
        .person-link { color: var(--uui-color-interactive); font-weight: 600; text-decoration: none; }
        .person-link:hover { color: var(--uui-color-interactive-emphasis); text-decoration: underline; }
        input[type="file"] { max-width: 100%; }
        dialog.preview { width: min(900px, 92vw); max-height: 90vh; padding: 0; border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); box-shadow: var(--uui-shadow-depth-5); }
        dialog.preview::backdrop { background: rgba(0, 0, 0, 0.5); }
        .preview-head, .preview-foot { display: flex; align-items: center; justify-content: space-between; gap: var(--uui-size-space-4); padding: var(--uui-size-space-4) var(--uui-size-space-5); }
        .preview-head { border-bottom: 1px solid var(--uui-color-border); }
        .preview-foot { border-top: 1px solid var(--uui-color-border); }
        .preview-body { display: flex; align-items: center; justify-content: center; background: var(--uui-color-surface-alt); min-height: 200px; max-height: 70vh; overflow: auto; }
        .preview-body img { max-width: 100%; max-height: 70vh; object-fit: contain; }
        .preview-broken { margin: 0; padding: var(--uui-size-space-6); text-align: center; color: var(--uui-color-text-alt); }
        .preview-body iframe { width: 100%; height: 70vh; border: 0; }
        .save-state { margin-left: auto; color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); }
    `];
}

export default MediaGovernanceImageViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-image-view": MediaGovernanceImageViewElement;
    }
}
