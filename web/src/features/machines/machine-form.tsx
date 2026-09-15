"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Save } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { Button, Card, Checkbox, ErrorState, Field, Input, LoadingSkeleton, PageHeader, PermissionState, Select, Textarea } from "@/components/ui";
import { useToast } from "@/components/ui/toast";
import { api, errorMessage } from "@/lib/api-client";
import { useReferenceData } from "@/lib/reference-data";
import type { Machine } from "@/types/api";

const optionalText = z.string().trim().optional().transform((value) => value || null);
const optionalId = z.string().optional().transform((value) => value || null);
const schema = z.object({
  machineCode: z.string().trim().min(1, "Machine code is required").max(50), assetNumber: optionalText, name: z.string().trim().min(1, "Name is required").max(150), categoryId: optionalId,
  manufacturer: optionalText, model: optionalText, serialNumber: optionalText, departmentId: optionalId, locationId: optionalId,
  installationDate: optionalText, commissioningDate: optionalText, warrantyExpiryDate: optionalText,
  machineOwnerUserId: optionalId, supervisorUserId: optionalId, calibrationRequired: z.boolean(), preventiveMaintenanceRequired: z.boolean(),
  status: z.enum(["Operational", "UnderMaintenance", "Breakdown", "OutOfService", "Standby", "Decommissioned"]), criticality: z.enum(["Low", "Medium", "High", "Critical"]),
  notes: optionalText, isActive: z.boolean(), assignmentReason: optionalText,
}).refine((value) => !value.commissioningDate || !value.installationDate || value.commissioningDate >= value.installationDate, { message: "Commissioning cannot be before installation", path: ["commissioningDate"] });
type FormInput = z.input<typeof schema>;
type Values = z.output<typeof schema>;
const defaults: FormInput = { machineCode: "", assetNumber: "", name: "", categoryId: "", manufacturer: "", model: "", serialNumber: "", departmentId: "", locationId: "", installationDate: "", commissioningDate: "", warrantyExpiryDate: "", machineOwnerUserId: "", supervisorUserId: "", calibrationRequired: false, preventiveMaintenanceRequired: true, status: "Operational", criticality: "Medium", notes: "", isActive: true, assignmentReason: "" };

export function MachineForm({ machineId }: { machineId?: string }) {
  const router = useRouter(); const client = useQueryClient(); const { showToast } = useToast();
  const refs = useReferenceData();
  const detail = useQuery({ queryKey: ["machines", machineId], queryFn: ({ signal }) => api.get<Machine>(`/machines/${machineId}`, signal), enabled: Boolean(machineId) });
  const { register, handleSubmit, reset, control, formState: { errors, isSubmitting } } = useForm<FormInput, unknown, Values>({ resolver: zodResolver(schema), defaultValues: defaults });
  const departmentId = useWatch({ control, name: "departmentId" });
  useEffect(() => { if (detail.data) reset({ ...defaults, ...detail.data, assetNumber: detail.data.assetNumber ?? "", categoryId: detail.data.categoryId ?? "", manufacturer: detail.data.manufacturer ?? "", model: detail.data.model ?? "", serialNumber: detail.data.serialNumber ?? "", departmentId: detail.data.departmentId ?? "", locationId: detail.data.locationId ?? "", installationDate: detail.data.installationDate ?? "", commissioningDate: detail.data.commissioningDate ?? "", warrantyExpiryDate: detail.data.warrantyExpiryDate ?? "", machineOwnerUserId: detail.data.machineOwnerUserId ?? "", supervisorUserId: detail.data.supervisorUserId ?? "", notes: detail.data.notes ?? "", assignmentReason: "" }); }, [detail.data, reset]);
  const save = useMutation({ mutationFn: (values: Values) => machineId ? api.put<Machine>(`/machines/${machineId}`, values) : api.post<Machine>("/machines", values), onSuccess: (machine) => { client.invalidateQueries({ queryKey: ["machines"] }); showToast(machineId ? "Machine updated" : "Machine created"); router.push(`/machines/${machine.id}`); } });
  if (!refs.canManageMachines) return <PermissionState description="Only managers and administrators can create or edit machines." />;
  if (machineId && detail.isPending) return <LoadingSkeleton rows={8} />;
  if (detail.error) return <ErrorState error={detail.error} onRetry={() => detail.refetch()} />;
  const filteredLocations = refs.locations.data?.filter((item) => !departmentId || item.departmentId === departmentId) ?? [];
  const submit = handleSubmit((values) => save.mutateAsync(values).catch(() => undefined));
  return <div className="page-stack"><PageHeader eyebrow="Asset master" title={machineId ? "Edit machine" : "Add machine"} description="Keep identification, responsibility, and compliance details current." actions={<Link className="button button-secondary button-md" href={machineId ? `/machines/${machineId}` : "/machines"}><ArrowLeft size={16} /> Back</Link>} />{save.error && <div className="form-alert" role="alert">{errorMessage(save.error)}</div>}<form onSubmit={submit} className="form-stack" noValidate>
    <FormSection title="Identification" description="Core asset identifiers and manufacturer details"><Field label="Machine code" required error={errors.machineCode?.message}><Input {...register("machineCode")} /></Field><Field label="Asset number"><Input {...register("assetNumber")} /></Field><Field label="Machine name" required error={errors.name?.message}><Input {...register("name")} /></Field><Field label="Category"><Select {...register("categoryId")}><option value="">Not set</option>{refs.categories.data?.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</Select></Field><Field label="Manufacturer"><Input {...register("manufacturer")} /></Field><Field label="Model"><Input {...register("model")} /></Field><Field label="Serial number"><Input {...register("serialNumber")} /></Field></FormSection>
    <FormSection title="Location" description="Where the machine belongs and operates"><Field label="Department"><Select {...register("departmentId")}><option value="">Not set</option>{refs.departments.data?.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</Select></Field><Field label="Location"><Select {...register("locationId")}><option value="">Not set</option>{filteredLocations.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</Select></Field></FormSection>
    <FormSection title="Dates" description="Installation, commissioning, and warranty milestones"><Field label="Installation date"><Input type="date" {...register("installationDate")} /></Field><Field label="Commissioning date" error={errors.commissioningDate?.message}><Input type="date" {...register("commissioningDate")} /></Field><Field label="Warranty expiry"><Input type="date" {...register("warrantyExpiryDate")} /></Field></FormSection>
    <FormSection title="Responsibility" description="Operational owner and approving supervisor"><Field label="Owner technician"><Select {...register("machineOwnerUserId")}><option value="">Unassigned</option>{refs.technicians.data?.map((item) => <option key={item.id} value={item.id}>{item.employeeId} · {item.firstName} {item.lastName}</option>)}</Select></Field><Field label="Supervisor"><Select {...register("supervisorUserId")}><option value="">Unassigned</option>{refs.supervisors.data?.map((item) => <option key={item.id} value={item.id}>{item.employeeId} · {item.firstName} {item.lastName}</option>)}</Select></Field><Field label="Assignment reason"><Input {...register("assignmentReason")} /></Field></FormSection>
    <FormSection title="Compliance" description="Requirements that drive planned work"><Checkbox label="Calibration required" {...register("calibrationRequired")} /><Checkbox label="Preventive maintenance required" {...register("preventiveMaintenanceRequired")} /></FormSection>
    <FormSection title="Operations" description="Current operating classification"><Field label="Status"><Select {...register("status")}><option>Operational</option><option>UnderMaintenance</option><option>Breakdown</option><option>OutOfService</option><option>Standby</option><option>Decommissioned</option></Select></Field><Field label="Criticality"><Select {...register("criticality")}><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></Select></Field><Checkbox label="Active record" {...register("isActive")} /><Field label="Notes" className="field-span"><Textarea rows={4} {...register("notes")} /></Field></FormSection>
    <div className="form-actions"><Link className="button button-secondary button-md" href="/machines">Cancel</Link><Button type="submit" loading={isSubmitting || save.isPending}><Save size={16} /> {machineId ? "Save changes" : "Create machine"}</Button></div>
  </form></div>;
}

function FormSection({ title, description, children }: { title: string; description: string; children: React.ReactNode }) { return <Card className="form-section"><header><h2>{title}</h2><p>{description}</p></header><div className="form-grid">{children}</div></Card>; }
