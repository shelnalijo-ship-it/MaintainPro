"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Boxes, CalendarClock, CheckCircle2, CircleGauge, ClipboardCheck, Clock3, Siren, Stethoscope, Wrench } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Card, DateControl, EmptyState, ErrorState, FilterBar, KpiCard, LoadingSkeleton, PageHeader, SectionHeader, StatusBadge } from "@/components/ui";
import { api, queryString } from "@/lib/api-client";
import { formatDate, monthLabel } from "@/lib/format";
import type { ManagerDashboard, ReportingTrends } from "@/types/api";
import { useAuth } from "@/features/auth/auth-provider";

const chartColors = ["#165B3A", "#C99A3D", "#851B2B", "#C96F4A", "#4C738C"];

export function DashboardView() {
  const { hasRole } = useAuth();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const params = queryString({ From: from, To: to });
  const scope = hasRole("MANAGER", "ADMIN") ? "manager" : "supervisor";
  const dashboard = useQuery({ queryKey: ["dashboard", scope, from, to], queryFn: ({ signal }) => api.get<ManagerDashboard>(`/dashboard/${scope}${params}`, signal) });
  const trends = useQuery({ queryKey: ["dashboard", "trends", from, to], queryFn: ({ signal }) => api.get<ReportingTrends>(`/reports/trends${params}`, signal) });
  if (dashboard.isPending) return <><PageHeader eyebrow="Manager overview" title="Operations dashboard" description="Loading today’s maintenance picture…" /><LoadingSkeleton rows={8} /></>;
  if (dashboard.error || !dashboard.data) return <><PageHeader title="Operations dashboard" /><ErrorState error={dashboard.error} onRetry={() => dashboard.refetch()} /></>;
  const data = dashboard.data;
  const machineStatus = [
    { name: "Operational", value: data.machines.operationalMachines }, { name: "Maintenance", value: data.machines.machinesUnderMaintenance },
    { name: "Breakdown", value: data.machines.breakdownMachines }, { name: "Out of service", value: data.machines.outOfServiceMachines },
  ];
  return <div className="page-stack">
    <PageHeader eyebrow="Manager overview" title="What needs attention" description={`Status as of ${formatDate(data.period.asOf)} · ${data.scope} view`} actions={<Link className="button button-secondary button-md" href="/reports">Open reports</Link>} />
    <FilterBar onClear={() => { setFrom(""); setTo(""); }}><DateControl aria-label="From date" value={from} onChange={(event) => setFrom(event.target.value)} /><DateControl aria-label="To date" value={to} onChange={(event) => setTo(event.target.value)} /></FilterBar>
    <section className="kpi-grid">
      <KpiCard label="Total machines" value={data.machines.totalMachines} hint={`${data.machines.operationalMachines} operational`} icon={<Boxes />} />
      <KpiCard label="Due today" value={data.maintenance.maintenanceDueToday} tone="gold" icon={<CalendarClock />} />
      <KpiCard label="Due this week" value={data.maintenance.maintenanceDueThisWeek} tone="blue" icon={<Clock3 />} />
      <KpiCard label="Overdue" value={data.maintenance.maintenanceOverdue} tone="maroon" icon={<AlertTriangle />} />
      <KpiCard label="Awaiting approval" value={data.maintenance.maintenanceAwaitingApproval} tone="gold" icon={<ClipboardCheck />} />
      <KpiCard label="Escalated" value={data.maintenance.maintenanceEscalated} tone="terracotta" icon={<Siren />} />
      <KpiCard label="Open breakdowns" value={data.breakdowns.openBreakdowns} tone="maroon" icon={<Wrench />} />
      <KpiCard label="Critical breakdowns" value={data.breakdowns.criticalBreakdowns} tone="maroon" icon={<Siren />} />
      <KpiCard label="Calibration expired" value={data.calibration.calibrationExpired} tone="terracotta" icon={<Stethoscope />} />
      <KpiCard label="Expiring soon" value={data.calibration.calibrationExpiringWithin30Days} hint="Within 30 days" tone="gold" icon={<CircleGauge />} />
    </section>
    <section className="chart-grid">
      <Card className="chart-card"><SectionHeader title="Maintenance compliance" description="Approved work against all work due" /><div className="compliance-chart"><div className="compliance-ring" style={{ "--compliance": `${data.maintenanceCompliance.percentage ?? 0}%` } as React.CSSProperties}><span><strong>{data.maintenanceCompliance.percentage?.toFixed(1) ?? "—"}%</strong><small>compliant</small></span></div><div className="chart-legend"><span><i style={{ background: "#165B3A" }} />{data.maintenanceCompliance.completedCount} approved</span><span><i style={{ background: "#851B2B" }} />{data.maintenanceCompliance.overdueCount} overdue</span><span><i style={{ background: "#C99A3D" }} />{data.maintenanceCompliance.pendingCount} pending</span></div></div></Card>
      <Card className="chart-card"><SectionHeader title="Approved vs overdue" description="Monthly maintenance trend" /><ChartFrame loading={trends.isPending} error={trends.error}><ResponsiveContainer width="100%" height={250}><AreaChart data={trends.data?.maintenance.map((point) => ({ ...point, label: monthLabel(point.year, point.month) })) ?? []}><defs><linearGradient id="approved" x1="0" y1="0" x2="0" y2="1"><stop offset="5%" stopColor="#165B3A" stopOpacity={0.35}/><stop offset="95%" stopColor="#165B3A" stopOpacity={0}/></linearGradient></defs><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey="label"/><YAxis allowDecimals={false}/><Tooltip/><Legend/><Area type="monotone" dataKey="approved" stroke="#165B3A" fill="url(#approved)"/><Area type="monotone" dataKey="overdue" stroke="#851B2B" fill="transparent"/></AreaChart></ResponsiveContainer></ChartFrame></Card>
      <Card className="chart-card"><SectionHeader title="Technician workload" description="Assignments and approvals" /><ChartFrame><ResponsiveContainer width="100%" height={250}><BarChart data={data.technicianWorkload.slice(0, 8)} layout="vertical"><CartesianGrid strokeDasharray="3 3" horizontal={false}/><XAxis type="number" allowDecimals={false}/><YAxis dataKey="technicianName" type="category" width={92} tick={{ fontSize: 11 }}/><Tooltip/><Legend/><Bar dataKey="assigned" fill="#C99A3D" radius={[0,4,4,0]}/><Bar dataKey="approved" fill="#165B3A" radius={[0,4,4,0]}/><Bar dataKey="overdue" fill="#851B2B" radius={[0,4,4,0]}/></BarChart></ResponsiveContainer></ChartFrame></Card>
      <Card className="chart-card"><SectionHeader title="Machine status" description="Current asset availability" /><ChartFrame><ResponsiveContainer width="100%" height={250}><PieChart><Pie data={machineStatus} dataKey="value" nameKey="name" innerRadius={55} outerRadius={88} paddingAngle={3}>{machineStatus.map((entry, index) => <Cell key={entry.name} fill={chartColors[index]} />)}</Pie><Tooltip/><Legend/></PieChart></ResponsiveContainer></ChartFrame></Card>
      <Card className="chart-card"><SectionHeader title="Breakdown trend" description="Count and downtime by month" /><ChartFrame loading={trends.isPending} error={trends.error}><ResponsiveContainer width="100%" height={250}><BarChart data={trends.data?.breakdowns.map((point) => ({ ...point, label: monthLabel(point.year, point.month) })) ?? []}><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey="label"/><YAxis allowDecimals={false}/><Tooltip/><Bar dataKey="breakdownCount" name="Breakdowns" fill="#851B2B" radius={[4,4,0,0]}/></BarChart></ResponsiveContainer></ChartFrame></Card>
      <Card className="chart-card"><SectionHeader title="Calibration distribution" description="Validity across calibrated assets" /><ChartFrame loading={trends.isPending} error={trends.error}><ResponsiveContainer width="100%" height={250}><PieChart><Pie data={trends.data?.calibrationStatus ?? []} dataKey="count" nameKey="status" outerRadius={88}>{trends.data?.calibrationStatus.map((entry, index) => <Cell key={entry.status} fill={chartColors[index % chartColors.length]} />)}</Pie><Tooltip/><Legend/></PieChart></ResponsiveContainer></ChartFrame></Card>
    </section>
    <section className="attention-grid">
      <Card><SectionHeader title="Escalation alerts" description="Highest urgency work first" action={<Link href="/work-orders?overdue=true">View work orders</Link>} />{data.escalationAlerts.length ? <div className="attention-list">{data.escalationAlerts.map((item) => <Link href={`/work-orders/${item.workOrderId}`} key={item.workOrderId}><div><strong>{item.workOrderNumber}</strong><span>{item.machineCode} · {item.machineName}</span></div><div><StatusBadge value={`Level ${item.escalationLevel}`} /><small>Due {formatDate(item.dueDate)}</small></div></Link>)}</div> : <EmptyState title="No active escalations" description="Escalation alerts will appear here." />}</Card>
      <Card><SectionHeader title="Upcoming calibration" description="Expiry dates approaching" action={<Link href="/calibration">View calibration</Link>} />{data.upcomingCalibrations.length ? <div className="attention-list">{data.upcomingCalibrations.map((item) => <Link href={`/machines/${item.machineId}`} key={item.machineId}><div><strong>{item.machineCode}</strong><span>{item.machineName}</span></div><div><StatusBadge value={item.validityStatus} /><small>{formatDate(item.expiryDate)}</small></div></Link>)}</div> : <EmptyState title="No upcoming expiry" description="Calibration renewals due soon will appear here." icon={<CheckCircle2 size={30} />} />}</Card>
      <Card><SectionHeader title="External service follow-ups" description={`${data.externalServiceFollowUpsDue} follow-up${data.externalServiceFollowUpsDue === 1 ? "" : "s"} due`} action={<Link href="/external-services">View services</Link>} />{data.externalServiceFollowUps.length ? <div className="attention-list">{data.externalServiceFollowUps.map((item) => <Link href={`/external-services/${item.externalServiceId}`} key={item.externalServiceId}><div><strong>{item.serviceNumber}</strong><span>{item.serviceCompany} · {item.machineCode}</span></div><div><StatusBadge value={item.overdue ? "Overdue" : "Upcoming"} /><small>{formatDate(item.dueDate)}</small></div></Link>)}</div> : <EmptyState title="No service follow-ups" description="Scheduled vendor follow-ups will appear here." />}</Card>
    </section>
  </div>;
}

function ChartFrame({ children, loading, error }: { children: React.ReactNode; loading?: boolean; error?: unknown }) { if (loading) return <LoadingSkeleton rows={3} />; if (error) return <ErrorState error={error} />; return <div className="chart-body">{children}</div>; }
