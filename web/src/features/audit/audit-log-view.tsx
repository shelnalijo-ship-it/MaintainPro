"use client";

import { useQuery } from "@tanstack/react-query";
import { Eye, FileClock } from "lucide-react";
import { useMemo, useState } from "react";
import { Button, DateControl, Drawer, EmptyState, ErrorState, Field, FilterBar, Input, LoadingSkeleton, PageHeader, Pagination, PermissionState, Table, type TableColumn } from "@/components/ui";
import { useAuth } from "@/features/auth/auth-provider";
import { api, queryString } from "@/lib/api-client";
import { formatDateTime, nameOf } from "@/lib/format";
import type { AuditLog, PagedResult } from "@/types/api";

const emptyFilters = { from: "", to: "", userId: "", action: "", entityType: "", entityId: "" };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const sensitiveKey = /password|token|secret|signing.?key|credential|authorization|api.?key|connection.?string/i;
const jwtValue = /(?:bearer\s+)?[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}/i;

function sanitizeAuditValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(sanitizeAuditValue);
  if (value && typeof value === "object") return Object.fromEntries(
    Object.entries(value).filter(([key]) => !sensitiveKey.test(key))
      .map(([key, item]) => [key, sanitizeAuditValue(item)]),
  );
  if (typeof value === "string" && jwtValue.test(value)) return "[REDACTED]";
  return value;
}

export function formatAuditJson(json?: string | null) {
  if (!json) return "No values recorded.";
  try { return JSON.stringify(sanitizeAuditValue(JSON.parse(json)), null, 2); }
  catch { return "Legacy value unavailable because it is not valid JSON."; }
}

function safeMetadata(value?: string | null) {
  if (!value || sensitiveKey.test(value) || jwtValue.test(value)) return "—";
  return value;
}

export function AuditLogView() {
  const { hasRole } = useAuth();
  const canView = hasRole("MANAGER", "ADMIN");
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState(emptyFilters);
  const [selected, setSelected] = useState<AuditLog>();
  const validUserId = !filters.userId || uuid.test(filters.userId.trim());
  const validDates = !filters.from || !filters.to || filters.from <= filters.to;
  const validFilters = validUserId && validDates;
  const logs = useQuery({
    queryKey: ["audit-logs", filters, page],
    queryFn: ({ signal }) => api.get<PagedResult<AuditLog>>(`/audit-logs${queryString({
      From: filters.from, To: filters.to, UserId: filters.userId.trim(), Action: filters.action,
      EntityType: filters.entityType, EntityId: filters.entityId, Page: page, PageSize: 20,
    })}`, signal),
    enabled: canView && validFilters,
    placeholderData: (previous) => previous,
  });
  const set = (name: keyof typeof emptyFilters, value: string) => {
    setFilters((current) => ({ ...current, [name]: value }));
    setPage(1);
  };
  const suggestions = useMemo(() => {
    const users = new Map<string, string>();
    for (const row of logs.data?.items ?? []) if (row.user) users.set(row.user.id, `${nameOf(row.user)} · ${row.user.employeeId}`);
    return [...users.entries()];
  }, [logs.data]);
  const columns: TableColumn<AuditLog>[] = [
    { key: "timestamp", header: "Timestamp", cell: (row) => <time dateTime={row.createdAt}>{formatDateTime(row.createdAt)}</time> },
    { key: "user", header: "User", cell: (row) => row.user ? <div className="primary-cell"><strong>{nameOf(row.user)}</strong><span>{row.user.employeeId}</span></div> : <span className="muted">{row.userId ?? "System"}</span> },
    { key: "action", header: "Action", cell: (row) => <code className="audit-code">{row.action}</code> },
    { key: "entityType", header: "Entity Type", cell: (row) => row.entityType },
    { key: "entityId", header: "Entity ID", cell: (row) => <span className="audit-entity-id">{row.entityId ?? "—"}</span> },
    { key: "details", header: "Details", className: "actions-cell", cell: (row) => <Button variant="ghost" size="sm" onClick={() => setSelected(row)}><Eye size={15}/> View</Button> },
  ];

  if (!canView) return <PermissionState description="Audit logs are restricted to managers and administrators." />;
  return <div className="page-stack">
    <PageHeader eyebrow="Administration" title="Audit logs" description="Review immutable changes to protected operational records." />
    <FilterBar onClear={() => { setFilters(emptyFilters); setPage(1); }}>
      <Field label="From"><DateControl aria-label="From date" value={filters.from} onChange={(event) => set("from", event.target.value)} /></Field>
      <Field label="To"><DateControl aria-label="To date" value={filters.to} onChange={(event) => set("to", event.target.value)} /></Field>
      <Field label="User ID" hint={!validUserId ? "Enter a complete user UUID." : undefined}>
        <Input aria-label="User ID" list="audit-user-suggestions" value={filters.userId} onChange={(event) => set("userId", event.target.value)} placeholder="User UUID" aria-invalid={!validUserId} />
        <datalist id="audit-user-suggestions">{suggestions.map(([id, label]) => <option key={id} value={id}>{label}</option>)}</datalist>
      </Field>
      <Field label="Action"><Input aria-label="Action" value={filters.action} onChange={(event) => set("action", event.target.value)} placeholder="Machine.Updated" /></Field>
      <Field label="Entity type"><Input aria-label="Entity type" value={filters.entityType} onChange={(event) => set("entityType", event.target.value)} placeholder="Machine" /></Field>
      <Field label="Entity ID"><Input aria-label="Entity ID" value={filters.entityId} onChange={(event) => set("entityId", event.target.value)} placeholder="Record identifier" /></Field>
    </FilterBar>
    {!validDates && <p className="field-error audit-filter-error" role="alert">From date must be on or before To date.</p>}
    {logs.isPending ? <LoadingSkeleton rows={8} /> : logs.error ? <ErrorState error={logs.error} onRetry={() => logs.refetch()} /> : !logs.data?.items.length ? <EmptyState icon={<FileClock size={34}/>} title="No audit records" description="No audit records match the selected filters." /> : <>
      <Table columns={columns} items={logs.data.items} rowKey={(row) => row.id} />
      <Pagination page={logs.data.page} totalPages={logs.data.totalPages} totalCount={logs.data.totalCount} onPageChange={setPage} />
    </>}
    <Drawer open={Boolean(selected)} title="Audit record details" onClose={() => setSelected(undefined)}>
      {selected && <div className="audit-detail">
        <dl className="definition-list">
          <div><dt>Timestamp</dt><dd>{formatDateTime(selected.createdAt)}</dd></div>
          <div><dt>User</dt><dd>{selected.user ? `${nameOf(selected.user)} (${selected.user.employeeId})` : selected.userId ?? "System"}</dd></div>
          <div><dt>Action</dt><dd>{selected.action}</dd></div>
          <div><dt>Entity type</dt><dd>{selected.entityType}</dd></div>
          <div><dt>Entity ID</dt><dd>{selected.entityId ?? "—"}</dd></div>
          <div><dt>IP address</dt><dd>{safeMetadata(selected.ipAddress)}</dd></div>
          <div className="field-span"><dt>Device</dt><dd>{safeMetadata(selected.deviceInfo)}</dd></div>
        </dl>
        <section><h3>Old values</h3><pre>{formatAuditJson(selected.oldValuesJson)}</pre></section>
        <section><h3>New values</h3><pre>{formatAuditJson(selected.newValuesJson)}</pre></section>
      </div>}
    </Drawer>
  </div>;
}
