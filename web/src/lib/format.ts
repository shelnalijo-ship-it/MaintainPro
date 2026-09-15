export function formatDate(value?: string | null) {
  if (!value) return "—";
  const date = new Date(value.length === 10 ? `${value}T00:00:00` : value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat("en-IN", { day: "2-digit", month: "short", year: "numeric" }).format(date);
}

export function formatDateTime(value?: string | null) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat("en-IN", { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function formatMinutes(value?: number | null) {
  if (value === undefined || value === null) return "—";
  if (value < 60) return `${Math.round(value)} min`;
  return `${Math.floor(value / 60)}h ${Math.round(value % 60)}m`;
}

export function formatMoney(value?: number | null) {
  return value === undefined || value === null ? "—" : new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 2 }).format(value);
}

export function nameOf(value: { firstName: string; lastName: string }) { return `${value.firstName} ${value.lastName}`.trim(); }
export function monthLabel(year: number, month: number) { return new Intl.DateTimeFormat("en-IN", { month: "short", year: "2-digit" }).format(new Date(year, month - 1, 1)); }
