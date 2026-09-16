"use client";

import { Activity, Bell, Boxes, Building2, CalendarRange, ChevronLeft, ChevronRight, ClipboardList, FileBarChart, FileClock, FileStack, Gauge, LogOut, Menu, Search, Settings, Stethoscope, Users, Wrench, X } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { useAuth } from "@/features/auth/auth-provider";
import type { Role } from "@/types/api";
import { ErrorState, LoadingSkeleton, PermissionState, cx } from "@/components/ui";
import { NotificationBell } from "@/features/notifications/notification-bell";

const nav = [
  { href: "/dashboard", label: "Dashboard", icon: Gauge },
  { href: "/machines", label: "Machines", icon: Boxes },
  { href: "/maintenance-plans", label: "Maintenance Plans", icon: ClipboardList, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] as Role[] },
  { href: "/work-orders", label: "Work Orders", icon: Wrench },
  { href: "/calendar", label: "Calendar", icon: CalendarRange },
  { href: "/breakdowns", label: "Breakdowns", icon: Activity },
  { href: "/calibration", label: "Calibration", icon: Stethoscope },
  { href: "/external-services", label: "External Services", icon: Building2 },
  { href: "/documents", label: "Documents", icon: FileStack },
  { href: "/reports", label: "Reports", icon: FileBarChart, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] as Role[] },
  { href: "/employees", label: "Employees", icon: Users, roles: ["ADMIN"] as Role[] },
  { href: "/audit-logs", label: "Audit Logs", icon: FileClock, roles: ["MANAGER", "ADMIN"] as Role[] },
  { href: "/settings", label: "Settings", icon: Settings, roles: ["MANAGER", "ADMIN"] as Role[] },
];

export function PortalShell({ children }: { children: ReactNode }) {
  const { user, status, error, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [search, setSearch] = useState("");
  useEffect(() => { if (status === "unauthenticated") router.replace("/login"); }, [status, router]);
  if (status === "loading") return <main className="center-screen"><div className="brand-mark"><span>M</span></div><LoadingSkeleton rows={3} /></main>;
  if (error) return <main className="center-screen wide"><ErrorState error={error} onRetry={() => window.location.reload()} /></main>;
  if (!user) return <main className="center-screen"><p>Redirecting to sign in…</p></main>;
  if (!user.isActive) return <main className="center-screen wide"><PermissionState description="This account is inactive. Contact an administrator." /></main>;
  if (!user.roles.some((role) => ["MANAGER", "ADMIN", "SUPERVISOR"].includes(role))) return <main className="center-screen wide"><PermissionState description="The manager web portal is available to managers, administrators, and permitted supervisors." /></main>;
  const visibleNav = nav.filter((item) => !item.roles || user.roles.some((role) => item.roles?.includes(role)));
  const submitSearch = (event: React.FormEvent) => { event.preventDefault(); const value = search.trim(); if (value) router.push(`/machines?search=${encodeURIComponent(value)}`); };
  return <div className={cx("portal", collapsed && "sidebar-collapsed", mobileOpen && "mobile-nav-open")}>
    <aside className="sidebar"><div className="sidebar-brand"><div className="brand-mark"><span>M</span></div><div><strong>MaintainPro</strong><span>Operations portal</span></div><button className="mobile-close" aria-label="Close navigation" onClick={() => setMobileOpen(false)}><X size={20} /></button></div><nav aria-label="Primary navigation">{visibleNav.map((item) => { const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(`${item.href}/`)); const Icon = item.icon; return <Link key={item.href} href={item.href} onClick={() => setMobileOpen(false)} className={cx(active && "active")} title={collapsed ? item.label : undefined}><Icon size={19} /><span>{item.label}</span>{active && <i />}</Link>; })}</nav><div className="sidebar-foot"><div className="kasavu-line" /><span>Maintain. Comply. Improve.</span></div><button className="collapse-button" aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"} onClick={() => setCollapsed((value) => !value)}>{collapsed ? <ChevronRight size={17} /> : <><ChevronLeft size={17} /><span>Collapse</span></>}</button></aside>
    <div className="mobile-backdrop" onClick={() => setMobileOpen(false)} />
    <section className="portal-main"><header className="topbar"><button className="icon-button menu-button" aria-label="Open navigation" onClick={() => setMobileOpen(true)}><Menu size={21} /></button><form className="global-search" onSubmit={submitSearch}><Search size={18} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search machines by code or name…" aria-label="Global machine search" /><kbd>↵</kbd></form><div className="topbar-actions"><Link className="icon-button notification-link" href="/notifications" aria-label="Open notification page"><Bell size={19} /></Link><NotificationBell /><div className="user-summary"><div className="avatar">{user.firstName[0]}{user.lastName[0]}</div><div><strong>{user.firstName} {user.lastName}</strong><span>{user.roles.join(" · ")}</span></div></div><button className="icon-button" aria-label="Log out" title="Log out" onClick={() => void logout().finally(() => router.replace("/login"))}><LogOut size={19} /></button></div></header><main className="content">{children}</main></section>
  </div>;
}
