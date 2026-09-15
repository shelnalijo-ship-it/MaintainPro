"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createContext, useContext, type ReactNode } from "react";
import { api, ApiError } from "@/lib/api-client";
import type { AuthResponse, Role, User } from "@/types/api";

interface AuthContextValue {
  user: User | null;
  status: "loading" | "authenticated" | "unauthenticated";
  error: unknown;
  login: (identifier: string, password: string) => Promise<User>;
  logout: () => Promise<void>;
  hasRole: (...roles: Role[]) => boolean;
}
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const me = useQuery({ queryKey: ["auth", "me"], queryFn: ({ signal }) => api.get<User>("/auth/me", signal), retry: false, staleTime: 60_000 });
  const loginMutation = useMutation({ mutationFn: ({ identifier, password }: { identifier: string; password: string }) => api.post<AuthResponse>("/auth/login", { identifier, password }) });
  const logoutMutation = useMutation({ mutationFn: () => api.post<void>("/auth/logout") });
  const user = me.data ?? null;
  const unauthenticated = me.error instanceof ApiError && me.error.status === 401;

  return <AuthContext.Provider value={{
    user,
    status: me.isPending ? "loading" : user ? "authenticated" : unauthenticated || me.isError ? "unauthenticated" : "loading",
    error: unauthenticated ? null : me.error,
    login: async (identifier, password) => { const response = await loginMutation.mutateAsync({ identifier, password }); queryClient.setQueryData(["auth", "me"], response.user); return response.user; },
    logout: async () => { try { await logoutMutation.mutateAsync(); } finally { queryClient.clear(); } },
    hasRole: (...roles) => Boolean(user?.roles.some((role) => roles.includes(role))),
  }}>{children}</AuthContext.Provider>;
}

export function useAuth() { const context = useContext(AuthContext); if (!context) throw new Error("useAuth must be used within AuthProvider"); return context; }
