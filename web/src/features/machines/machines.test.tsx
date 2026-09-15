import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { jsonResponse, renderWithProviders } from "@/test/test-utils";
import { MachineForm } from "./machine-form";
import { MachineList } from "./machine-list";

const push = vi.fn();
const authState = vi.hoisted(() => ({ canManage: true }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push, replace: vi.fn() }), useSearchParams: () => new URLSearchParams() }));
vi.mock("@/features/auth/auth-provider", () => ({ useAuth: () => ({ hasRole: () => authState.canManage }) }));

const machine = { id: "11111111-1111-1111-1111-111111111111", machineCode: "MC-100", assetNumber: "A-2", name: "Main Pump", categoryId: "c1", manufacturer: "Kerala Works", model: "P10", serialNumber: "S10", departmentId: "d1", locationId: "l1", installationDate: "2024-01-01", commissioningDate: "2024-01-02", warrantyExpiryDate: null, status: "Operational", criticality: "High", calibrationRequired: true, preventiveMaintenanceRequired: true, machineOwnerUserId: "t1", supervisorUserId: "s1", notes: null, isActive: true, createdAt: "2026-01-01", updatedAt: "2026-01-01", currentCalibrationStatus: "VALID", currentCertificateNumber: "CAL-1", calibrationExpiryDate: "2027-01-01", daysUntilCalibrationExpiry: 100, renewalInProgress: false };

function mockApi() {
  return vi.spyOn(globalThis, "fetch").mockImplementation((input, init) => {
    const url = String(input);
    if (url.includes("/departments")) return jsonResponse([{ id: "d1", name: "Production", isActive: true }]);
    if (url.includes("/locations")) return jsonResponse([{ id: "l1", name: "Line 1", departmentId: "d1", isActive: true }]);
    if (url.includes("/machine-categories")) return jsonResponse([{ id: "c1", name: "Pump", isActive: true }]);
    if (url.includes("/users/technicians")) return jsonResponse([{ id: "t1", employeeId: "T1", firstName: "Anu", lastName: "Jose" }]);
    if (url.includes("/users/supervisors")) return jsonResponse([{ id: "s1", employeeId: "S1", firstName: "Ravi", lastName: "Nair" }]);
    if (url.endsWith(`/machines/${machine.id}`) && init?.method === "PUT") return jsonResponse({ ...machine, name: "Updated Pump" });
    if (url.endsWith(`/machines/${machine.id}`)) return jsonResponse(machine);
    if (url.includes("/machines?")) return jsonResponse({ items: [machine], page: url.includes("Page=2") ? 2 : 1, pageSize: 20, totalCount: 21, totalPages: 2 });
    return jsonResponse({});
  });
}

describe("machines", () => {
  beforeEach(() => { vi.restoreAllMocks(); push.mockReset(); authState.canManage = true; });
  it("lists machines, applies a search filter, and pages", async () => {
    const fetchMock = mockApi();
    renderWithProviders(<MachineList />);
    expect(await screen.findByText("MC-100")).toBeInTheDocument();
    await userEvent.type(screen.getByPlaceholderText(/code, name/i), "pump");
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes("Search=pump"))).toBe(true));
    await userEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes("Page=2"))).toBe(true));
  });
  it("validates required fields before creating", async () => {
    mockApi(); renderWithProviders(<MachineForm />);
    await userEvent.click(screen.getByRole("button", { name: /create machine/i }));
    expect(await screen.findByText("Machine code is required")).toBeInTheDocument();
    expect(screen.getByText("Name is required")).toBeInTheDocument();
  });
  it("loads and edits a machine", async () => {
    const fetchMock = mockApi(); renderWithProviders(<MachineForm machineId={machine.id} />);
    const name = await screen.findByDisplayValue("Main Pump");
    await userEvent.clear(name); await userEvent.type(name, "Updated Pump");
    await userEvent.click(screen.getByRole("button", { name: /save changes/i }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith(machine.id) && init?.method === "PUT")).toBe(true));
    expect(push).toHaveBeenCalledWith(`/machines/${machine.id}`);
  });
  it("hides machine mutation actions for a supervisor", async () => {
    authState.canManage = false; mockApi(); renderWithProviders(<MachineList />);
    expect(await screen.findByText("MC-100")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /add machine/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/edit MC-100/i)).not.toBeInTheDocument();
  });
});
