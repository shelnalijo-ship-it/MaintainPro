"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type { MasterDataItem, UserLookup } from "@/types/api";
import { useAuth } from "@/features/auth/auth-provider";

export function useReferenceData() {
  const { hasRole } = useAuth();
  const canManageMachines = hasRole("MANAGER", "ADMIN");
  const departments = useQuery({ queryKey: ["departments", "active"], queryFn: ({ signal }) => api.get<MasterDataItem[]>("/departments?isActive=true", signal), enabled: canManageMachines });
  const locations = useQuery({ queryKey: ["locations", "active"], queryFn: ({ signal }) => api.get<MasterDataItem[]>("/locations?isActive=true", signal), enabled: canManageMachines });
  const categories = useQuery({ queryKey: ["machine-categories", "active"], queryFn: ({ signal }) => api.get<MasterDataItem[]>("/machine-categories?isActive=true", signal), enabled: canManageMachines });
  const technicians = useQuery({ queryKey: ["users", "technicians"], queryFn: ({ signal }) => api.get<UserLookup[]>("/users/technicians", signal), enabled: canManageMachines });
  const supervisors = useQuery({ queryKey: ["users", "supervisors"], queryFn: ({ signal }) => api.get<UserLookup[]>("/users/supervisors", signal), enabled: canManageMachines });
  return { departments, locations, categories, technicians, supervisors, canManageMachines };
}

export function lookupName(items: MasterDataItem[] | undefined, id?: string | null) { return items?.find((item) => item.id === id)?.name ?? (id ? `${id.slice(0, 8)}…` : "—"); }
export function lookupUser(items: UserLookup[] | undefined, id?: string | null) { const item = items?.find((entry) => entry.id === id); return item ? `${item.firstName} ${item.lastName}` : id ? `${id.slice(0, 8)}…` : "—"; }
