import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PortalShell } from "./portal-shell";

const replace = vi.fn();
const logout = vi.fn().mockResolvedValue(undefined);
let currentAuth: Record<string, unknown>;
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace, push: vi.fn() }), usePathname: () => "/dashboard" }));
vi.mock("@/features/auth/auth-provider", () => ({ useAuth: () => currentAuth }));
vi.mock("@/features/notifications/notification-bell", () => ({ NotificationBell: () => <button>Bell</button> }));

const user = { id: "1", employeeId: "M1", firstName: "Meera", lastName: "Nair", email: "m@example.com", isActive: true, roles: ["MANAGER"], createdAt: "", updatedAt: "" };

describe("PortalShell authorization", () => {
  beforeEach(() => { vi.clearAllMocks(); currentAuth = { user, status: "authenticated", error: null, logout, hasRole: (...roles: string[]) => user.roles.some((role) => roles.includes(role)) }; });
  it("redirects an unauthenticated visitor to login", async () => {
    currentAuth = { user: null, status: "unauthenticated", error: null, logout, hasRole: () => false };
    render(<PortalShell><p>Protected content</p></PortalShell>);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
  });
  it("hides admin-only navigation from supervisors", () => {
    const supervisor = { ...user, roles: ["SUPERVISOR"] };
    currentAuth = { user: supervisor, status: "authenticated", error: null, logout, hasRole: (...roles: string[]) => supervisor.roles.some((role) => roles.includes(role)) };
    render(<PortalShell><p>Protected content</p></PortalShell>);
    expect(screen.getByText("Reports")).toBeInTheDocument();
    expect(screen.queryByText("Employees")).not.toBeInTheDocument();
    expect(screen.queryByText("Settings")).not.toBeInTheDocument();
  });
  it("logs out through the session boundary", async () => {
    render(<PortalShell><p>Protected content</p></PortalShell>);
    await userEvent.click(screen.getByRole("button", { name: "Log out" }));
    await waitFor(() => expect(logout).toHaveBeenCalledOnce());
    expect(replace).toHaveBeenCalledWith("/login");
  });
});
