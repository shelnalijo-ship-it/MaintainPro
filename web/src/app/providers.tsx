"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { ToastProvider } from "@/components/ui/toast";
import { AuthProvider } from "@/features/auth/auth-provider";

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: (count, error) => !(error instanceof Error && "status" in error && (error as { status: number }).status < 500) && count < 2, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  }));
  return <QueryClientProvider client={queryClient}><ToastProvider><AuthProvider>{children}</AuthProvider></ToastProvider></QueryClientProvider>;
}
