import type { GovernanceStatus } from "../api/index.js";

export const STATUS_COLORS: Record<GovernanceStatus, "positive" | "warning" | "danger"> = {
  Compliant: "positive",
  ExpiringSoon: "warning",
  Expired: "danger",
  NoConsent: "danger",
  NeedsTagging: "warning",
};

/** 1843200 -> "1.8 MB". */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** "2026-10-29" -> "29 October 2026" (date-only values are parsed as local, never shifted by timezone). */
export function formatDate(iso: string | null): string {
  if (!iso) return "—";
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });
}

export const GIVEN_BY_OPTIONS: { value: string; label: string }[] = [
  { value: "Self", label: "The person themselves" },
  { value: "ParentOrGuardian", label: "Parent or guardian" },
  { value: "LegalRepresentative", label: "Legal representative" },
];

export const givenByLabel = (value: string | null) => GIVEN_BY_OPTIONS.find((o) => o.value === value)?.label ?? value ?? "";

/** Whole days from today until an ISO date (negative when it has passed). */
export function daysUntil(iso: string): number {
  const [y, m, d] = iso.split("-").map(Number);
  const target = new Date(y, m - 1, d).getTime();
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  return Math.round((target - today) / 86_400_000);
}

/** "in 15 months", "3 days ago", "today": a friendly distance for a number of days. */
export function relativeTime(days: number): string {
  if (days === 0) return "today";
  const n = Math.abs(days);
  const unit = n < 45 ? `${n} ${n === 1 ? "day" : "days"}` : n < 730 ? `${Math.round(n / 30.4)} months` : `${Math.round(n / 365)} years`;
  return days < 0 ? `${unit} ago` : `in ${unit}`;
}

export function relativeDays(days: number | null): string {
  if (days === null) return "";
  if (days === 0) return "today";
  return days < 0 ? `${-days} days ago` : `In ${days} days`;
}

export function todayIso(): string {
  const n = new Date();
  return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(n.getDate()).padStart(2, "0")}`;
}
