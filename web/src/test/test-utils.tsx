import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { ToastProvider } from "@/components/ui/toast";

export function renderWithProviders(ui: ReactElement, options?: Omit<RenderOptions, "wrapper">) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) { return <QueryClientProvider client={client}><ToastProvider>{children}</ToastProvider></QueryClientProvider>; }
  return { client, ...render(ui, { wrapper: Wrapper, ...options }) };
}

export function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(status === 204 ? null : JSON.stringify(body), { status, headers: { "content-type": "application/json" } }));
}

export const emptyPage = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
