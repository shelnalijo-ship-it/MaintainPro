import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { emptyPage, jsonResponse, renderWithProviders } from "@/test/test-utils";
import { AuditLogView, formatAuditJson } from "./audit-log-view";

let role: "ADMIN" | "MANAGER" | "SUPERVISOR" = "ADMIN";
vi.mock("@/features/auth/auth-provider", () => ({
  useAuth: () => ({ hasRole: (...roles: string[]) => roles.includes(role) }),
}));

const record = {
  id: "11111111-1111-4111-8111-111111111111",
  createdAt: "2026-09-15T08:30:00Z",
  userId: "22222222-2222-4222-8222-222222222222",
  user: { id: "22222222-2222-4222-8222-222222222222", employeeId: "MGR-01", firstName: "Maya", lastName: "Nair", email: "maya@example.invalid" },
  action: "Machine.Updated",
  entityType: "Machine",
  entityId: "33333333-3333-4333-8333-333333333333",
  oldValuesJson: JSON.stringify({ status: "Standby", PasswordHash: "hidden-secret", nested: { refreshToken: "hidden-token" } }),
  newValuesJson: JSON.stringify({ status: "Operational" }),
  ipAddress: "127.0.0.1",
  deviceInfo: "Test browser",
};
const page = { items: [record], page: 1, pageSize: 20, totalCount: 21, totalPages: 2 };

describe("AuditLogView", () => {
  beforeEach(() => { role = "ADMIN"; vi.restoreAllMocks(); });

  it("renders records, filters and pages through the audit API", async () => {
    const fetch = vi.spyOn(globalThis, "fetch").mockImplementation(() => jsonResponse(page));
    renderWithProviders(<AuditLogView />);

    expect(await screen.findByText("Machine.Updated")).toBeInTheDocument();
    expect(screen.getByText("Maya Nair")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Action"), { target: { value: "Machine.Updated" } });
    await waitFor(() => expect(fetch.mock.calls.some(([input]) => String(input).includes("Action=Machine.Updated"))).toBe(true));
    await userEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(fetch.mock.calls.some(([input]) => String(input).includes("Page=2"))).toBe(true));
  });

  it("opens readable details and removes secret-looking legacy fields", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(() => jsonResponse(page));
    renderWithProviders(<AuditLogView />);
    await userEvent.click(await screen.findByRole("button", { name: /view/i }));

    expect(screen.getByRole("dialog", { name: "Audit record details" })).toBeInTheDocument();
    expect(screen.getByText(/Standby/)).toBeInTheDocument();
    expect(screen.getByText(/Operational/)).toBeInTheDocument();
    expect(document.body).not.toHaveTextContent("hidden-secret");
    expect(document.body).not.toHaveTextContent("hidden-token");
    expect(formatAuditJson("not-json")).toBe("Legacy value unavailable because it is not valid JSON.");
  });

  it("shows empty and API error states", async () => {
    const fetch = vi.spyOn(globalThis, "fetch").mockImplementationOnce(() => jsonResponse(emptyPage));
    const first = renderWithProviders(<AuditLogView />);
    expect(await screen.findByText("No audit records")).toBeInTheDocument();
    first.unmount();

    fetch.mockImplementation(() => jsonResponse({ title: "Unavailable", detail: "Audit query failed" }, 500));
    renderWithProviders(<AuditLogView />);
    expect(await screen.findByText("Audit query failed")).toBeInTheDocument();
  });

  it("blocks roles outside manager and administrator", () => {
    role = "SUPERVISOR";
    const fetch = vi.spyOn(globalThis, "fetch");
    renderWithProviders(<AuditLogView />);

    expect(screen.getByText("Permission required")).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
});
