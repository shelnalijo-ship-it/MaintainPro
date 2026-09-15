import { screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithProviders, jsonResponse } from "@/test/test-utils";
import { DashboardView } from "./dashboard-view";

vi.mock("@/features/auth/auth-provider", () => ({ useAuth: () => ({ hasRole: (...roles: string[]) => roles.includes("MANAGER") }) }));
vi.mock("recharts", () => {
  const Container = ({ children }: { children?: ReactNode }) => <div>{children}</div>;
  const Chart = ({ children }: { children?: ReactNode }) => <svg>{children}</svg>;
  const Group = ({ children }: { children?: ReactNode }) => <g>{children}</g>;
  const Empty = () => <g />;
  return { ResponsiveContainer: Container, AreaChart: Chart, BarChart: Chart, PieChart: Chart, Pie: Group, Area: Empty, Bar: Empty, CartesianGrid: Empty, Cell: Empty, Legend: Empty, Tooltip: Empty, XAxis: Empty, YAxis: Empty };
});

const dashboard = {
  scope: "MANAGER", period: { from: "2026-09-01", to: "2026-09-30", asOf: "2026-09-15", dateConvention: "UTC" },
  machines: { totalMachines: 42, operationalMachines: 35, machinesUnderMaintenance: 3, breakdownMachines: 2, outOfServiceMachines: 2 },
  maintenance: { maintenanceDueToday: 4, maintenanceDueThisWeek: 11, maintenanceOverdue: 2, maintenanceAwaitingApproval: 5, maintenanceEscalated: 1 },
  breakdowns: { openBreakdowns: 3, criticalBreakdowns: 1 }, calibration: { calibrationValid: 20, calibrationExpiringWithin60Days: 6, calibrationExpiringWithin30Days: 3, calibrationExpiringWithin7Days: 1, calibrationExpired: 2, calibrationRenewalInProgress: 1 },
  externalServiceFollowUpsDue: 0, maintenanceCompliance: { percentage: 91.5, completedCount: 32, overdueCount: 2, pendingCount: 1, totalDueCount: 35 }, technicianWorkload: [], escalationAlerts: [], upcomingCalibrations: [], externalServiceFollowUps: [],
};
const trends = { period: dashboard.period, maintenance: [], breakdowns: [], technicianWorkload: [], calibrationStatus: [] };

describe("DashboardView", () => {
  beforeEach(() => { vi.restoreAllMocks(); });
  it("renders manager KPI values and empty attention panels", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation((input) => String(input).includes("/trends") ? jsonResponse(trends) : jsonResponse(dashboard));
    renderWithProviders(<DashboardView />);
    expect(await screen.findByText("42")).toBeInTheDocument();
    expect(screen.getByText("91.5%")).toBeInTheDocument();
    expect(screen.getByText("No active escalations")).toBeInTheDocument();
    expect(screen.getByText("No upcoming expiry")).toBeInTheDocument();
    expect(screen.getByText("No service follow-ups")).toBeInTheDocument();
  });
  it("shows an API error with retry", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation((input) => String(input).includes("/trends") ? jsonResponse(trends) : jsonResponse({ title: "Server error", detail: "Dashboard unavailable" }, 500));
    renderWithProviders(<DashboardView />);
    expect(await screen.findByText("Dashboard unavailable")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
  });
});
