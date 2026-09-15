"use client";

import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Pencil } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { Card, ErrorState, Input, LoadingSkeleton, PageHeader, SectionHeader, StatusBadge, Table, type TableColumn } from "@/components/ui";
import { useAuth } from "@/features/auth/auth-provider";
import { api } from "@/lib/api-client";
import { formatDate, formatDateTime } from "@/lib/format";
import { lookupUser, useReferenceData } from "@/lib/reference-data";
import type { ChecklistItem, ChecklistTemplate, Machine, MaintenancePlan } from "@/types/api";

export function PlanDetail({ id }: { id: string }) {
  const { hasRole } = useAuth(); const refs = useReferenceData(); const [version, setVersion] = useState("");
  const plan = useQuery({ queryKey: ["plans", id], queryFn: ({ signal }) => api.get<MaintenancePlan>(`/maintenance-plans/${id}`, signal) });
  const machine = useQuery({ queryKey: ["machines", plan.data?.machineId], queryFn: ({ signal }) => api.get<Machine>(`/machines/${plan.data?.machineId}`, signal), enabled: Boolean(plan.data?.machineId) });
  const checklist = useQuery({ queryKey: ["plans", id, "checklist", version], queryFn: ({ signal }) => api.get<ChecklistTemplate>(`/maintenance-plans/${id}/checklist${version ? `?version=${version}` : ""}`, signal) });
  if (plan.isPending || checklist.isPending) return <LoadingSkeleton rows={8}/>;
  if (plan.error || checklist.error || !plan.data) return <ErrorState error={plan.error ?? checklist.error}/>;
  const item = plan.data; const canManage = hasRole("MANAGER", "ADMIN");
  const columns: TableColumn<ChecklistItem>[] = [{ key: "sequence", header: "#", cell: (row) => row.sequenceNumber }, { key: "title", header: "Item", cell: (row) => <div className="primary-cell"><strong>{row.title}</strong><span>{row.description}</span></div> }, { key: "response", header: "Response", cell: (row) => row.responseType.replaceAll("_", " ") }, { key: "limits", header: "Unit / limits", cell: (row) => row.unit ? `${row.unit}${row.minimumValue != null || row.maximumValue != null ? ` · ${row.minimumValue ?? "—"}–${row.maximumValue ?? "—"}` : ""}` : "—" }, { key: "rules", header: "Rules", cell: (row) => <div className="badge-row">{row.isMandatory && <StatusBadge value="Mandatory"/>}{row.photoRequired && <StatusBadge value="Photo required"/>}</div> }];
  return <div className="page-stack"><PageHeader eyebrow="Maintenance plan" title={item.planName} description={`${machine.data?.machineCode ?? "Machine"} · Next due ${formatDate(item.nextDueDate)}`} actions={<><Link className="button button-secondary button-md" href="/maintenance-plans"><ArrowLeft size={16}/> Plans</Link>{canManage && <Link className="button button-primary button-md" href={`/maintenance-plans/${id}/edit`}><Pencil size={16}/> Edit</Link>}</>} /><div className="record-status"><StatusBadge value={item.priority}/><StatusBadge value={item.isActive}/><StatusBadge value={`${item.frequencyValue} ${item.frequencyType}`}/></div><div className="detail-grid"><Card><SectionHeader title="Schedule"/><dl className="definition-list"><div><dt>Start date</dt><dd>{formatDate(item.startDate)}</dd></div><div><dt>Next due</dt><dd>{formatDate(item.nextDueDate)}</dd></div><div><dt>Frequency</dt><dd>Every {item.frequencyValue} {item.frequencyType.replaceAll("_", " ").toLowerCase()}</dd></div><div><dt>Estimated duration</dt><dd>{item.estimatedDurationMinutes ? `${item.estimatedDurationMinutes} minutes` : "—"}</dd></div></dl></Card><Card><SectionHeader title="Assignment & evidence"/><dl className="definition-list"><div><dt>Technician</dt><dd>{lookupUser(refs.technicians.data, item.defaultTechnicianId)}</dd></div><div><dt>Supervisor</dt><dd>{lookupUser(refs.supervisors.data, item.supervisorId)}</dd></div><div><dt>Photo requirement</dt><dd>{item.photoRequired ? `${item.minimumPhotoCount} minimum` : "Not required"}</dd></div><div><dt>Comment</dt><dd>{item.commentRequired ? "Required" : "Optional"}</dd></div></dl></Card></div><Card><SectionHeader title="Instructions"/><p className="preserve-lines">{item.instructions || "No plan instructions."}</p></Card><Card><SectionHeader title={`Checklist · version ${checklist.data?.version ?? "—"}`} description="Generated work orders retain the exact version they were created from." action={<label className="version-control">Historical version <Input type="number" min="1" value={version} onChange={(event) => setVersion(event.target.value)} placeholder="Current"/></label>}/>{checklist.data ? <Table columns={columns} items={checklist.data.items} rowKey={(row) => row.id ?? String(row.sequenceNumber)}/> : null}<small>Created {formatDateTime(checklist.data?.createdAt)}</small></Card></div>;
}
