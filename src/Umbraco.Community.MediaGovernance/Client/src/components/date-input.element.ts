import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { formatDate, todayIso } from "../dashboard/format.js";

const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const WEEKDAYS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];
const pad = (n: number) => String(n).padStart(2, "0");
const toIso = (y: number, m: number, d: number) => `${y}-${pad(m + 1)}-${pad(d)}`;

let nextId = 0;

/**
 * A date field with a calendar that stands out from the page (bordered, shadowed Umbraco popover),
 * unlike the browser's native date picker which cannot be styled.
 * Value is an ISO date ("yyyy-MM-dd") or "". Fires `change` with the new value in `detail`.
 */
@customElement("umb-media-governance-date-input")
export class MediaGovernanceDateInputElement extends UmbLitElement {
    @property() value = "";
    @property() label = "Date";
    @state() private _year = new Date().getFullYear();
    @state() private _month = new Date().getMonth();

    readonly #popoverId = `mg-date-popover-${nextId++}`;

    #syncViewToValue() {
        const source = this.value || todayIso();
        const [y, m] = source.split("-").map(Number);
        this._year = y;
        this._month = m - 1;
    }

    #onToggle(e: Event) {
        if ((e as ToggleEvent).newState === "open") this.#syncViewToValue();
    }

    #step(delta: number) {
        const d = new Date(this._year, this._month + delta, 1);
        this._year = d.getFullYear();
        this._month = d.getMonth();
    }

    #pick(value: string) {
        this.value = value;
        this.dispatchEvent(new CustomEvent<string>("change", { detail: value }));
        (this.shadowRoot?.querySelector("uui-popover-container") as HTMLElement | null)?.hidePopover();
    }

    override render() {
        return html`
            <uui-button look="outline" popovertarget=${this.#popoverId} label=${this.label} class="trigger">
                <uui-icon name="icon-calendar-alt"></uui-icon>
                <span class=${this.value ? "" : "placeholder"}>${this.value ? formatDate(this.value) : "Select a date"}</span>
            </uui-button>
            <uui-popover-container id=${this.#popoverId} placement="bottom-start" @toggle=${this.#onToggle}>
                <umb-popover-layout>${this.#renderCalendar()}</umb-popover-layout>
            </uui-popover-container>
        `;
    }

    #renderCalendar() {
        const first = new Date(this._year, this._month, 1);
        const offset = (first.getDay() + 6) % 7; // Monday first
        const days = new Date(this._year, this._month + 1, 0).getDate();
        const today = todayIso();

        const cells = [
            ...Array.from({ length: offset }, () => html`<span></span>`),
            ...Array.from({ length: days }, (_, i) => {
                const iso = toIso(this._year, this._month, i + 1);
                return html`<button
                    type="button"
                    class="day ${iso === this.value ? "selected" : ""} ${iso === today ? "today" : ""}"
                    aria-label=${formatDate(iso)}
                    @click=${() => this.#pick(iso)}>
                    ${i + 1}
                </button>`;
            }),
        ];

        return html`
            <div class="calendar">
                <div class="header">
                    <uui-button compact look="secondary" label="Previous month" @click=${() => this.#step(-1)}><uui-icon name="icon-arrow-left"></uui-icon></uui-button>
                    <strong>${MONTHS[this._month]} ${this._year}</strong>
                    <uui-button compact look="secondary" label="Next month" @click=${() => this.#step(1)}><uui-icon name="icon-arrow-right"></uui-icon></uui-button>
                </div>
                <div class="grid weekdays">${WEEKDAYS.map((d) => html`<span>${d}</span>`)}</div>
                <div class="grid">${cells}</div>
                <div class="footer">
                    ${this.value ? html`<uui-button compact look="secondary" label="Clear" @click=${() => this.#pick("")}>Clear</uui-button>` : html`<span></span>`}
                    <uui-button compact look="secondary" label="Today" @click=${() => this.#pick(today)}>Today</uui-button>
                </div>
            </div>
            ${nothing}
        `;
    }

    static override styles = css`
        :host { display: inline-block; }
        .trigger { --uui-button-contrast: var(--uui-color-text); min-width: 220px; justify-content: flex-start; }
        .trigger span { margin-left: var(--uui-size-space-2); }
        .placeholder { color: var(--uui-color-text-alt); }
        uui-popover-container { --uui-popover-container-shadow: none; }
        .calendar { padding: var(--uui-size-space-4); width: 280px; box-sizing: border-box; }
        .header { display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--uui-size-space-4); }
        .grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 2px; text-align: center; }
        .weekdays { color: var(--uui-color-text-alt); font-size: var(--uui-type-small-size); margin-bottom: var(--uui-size-space-2); }
        .day { all: unset; box-sizing: border-box; cursor: pointer; height: 34px; display: flex; align-items: center; justify-content: center; border-radius: var(--uui-border-radius); }
        .day:hover { background: var(--uui-color-surface-emphasis); }
        .day:focus-visible { outline: 2px solid var(--uui-color-focus); }
        .day.today { border: 1px solid var(--uui-color-border-emphasis); }
        .day.selected { background: var(--uui-color-selected); color: var(--uui-color-selected-contrast); font-weight: 700; }
        .footer { display: flex; justify-content: space-between; margin-top: var(--uui-size-space-4); padding-top: var(--uui-size-space-3); border-top: 1px solid var(--uui-color-border); }
    `;
}

export default MediaGovernanceDateInputElement;

declare global {
    interface HTMLElementTagNameMap {
        "umb-media-governance-date-input": MediaGovernanceDateInputElement;
    }
}
