import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LoginForm } from "./login-form";

const replace = vi.fn();
const login = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace, push: vi.fn() }) }));
vi.mock("./auth-provider", () => ({ useAuth: () => ({ login }) }));

describe("LoginForm", () => {
  beforeEach(() => { vi.clearAllMocks(); });
  it("validates required credentials before submitting", async () => {
    render(<LoginForm />);
    fireEvent.click(screen.getByRole("button", { name: /sign in/i }));
    expect(await screen.findByText("Enter your email or employee ID")).toBeInTheDocument();
    expect(screen.getByText("Enter your password")).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });
  it("logs in an active manager and opens the dashboard", async () => {
    login.mockResolvedValue({ isActive: true, roles: ["MANAGER"] });
    render(<LoginForm />);
    await userEvent.type(screen.getByLabelText(/email or employee id/i), "manager@example.com");
    await userEvent.type(screen.getByLabelText(/^password/i), "correct-password");
    await userEvent.click(screen.getByRole("button", { name: /sign in/i }));
    await waitFor(() => expect(login).toHaveBeenCalledWith("manager@example.com", "correct-password"));
    expect(replace).toHaveBeenCalledWith("/dashboard");
  });
  it("shows inactive-user handling", async () => {
    login.mockResolvedValue({ isActive: false, roles: ["MANAGER"] });
    render(<LoginForm />);
    await userEvent.type(screen.getByLabelText(/email or employee id/i), "inactive");
    await userEvent.type(screen.getByLabelText(/^password/i), "correct-password");
    await userEvent.click(screen.getByRole("button", { name: /sign in/i }));
    expect(await screen.findByRole("alert")).toHaveTextContent(/inactive/i);
  });
});
