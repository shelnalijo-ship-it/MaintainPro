"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, CheckCheck } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api } from "@/lib/api-client";
import { formatDateTime } from "@/lib/format";
import type { Notification, PagedResult } from "@/types/api";
import { Button, EmptyState, ErrorState, LoadingSkeleton, StatusBadge } from "@/components/ui";

function target(notification: Notification) {
  if (!notification.entityId) return "/notifications";
  const type = notification.entityType?.toLowerCase() ?? "";
  if (type.includes("workorder")) return `/work-orders/${notification.entityId}`;
  if (type.includes("breakdown")) return `/breakdowns/${notification.entityId}`;
  if (type.includes("machine")) return `/machines/${notification.entityId}`;
  if (type.includes("external")) return `/external-services/${notification.entityId}`;
  return "/notifications";
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const client = useQueryClient();
  const router = useRouter();
  const count = useQuery({ queryKey: ["notifications", "unread-count"], queryFn: ({ signal }) => api.get<{ unreadCount: number }>("/notifications/unread-count", signal), refetchInterval: 60_000 });
  const list = useQuery({ queryKey: ["notifications", "bell"], queryFn: ({ signal }) => api.get<PagedResult<Notification>>("/notifications?Page=1&PageSize=6", signal), enabled: open });
  const markRead = useMutation({ mutationFn: (id: string) => api.patch(`/notifications/${id}/read`, {}), onSuccess: () => client.invalidateQueries({ queryKey: ["notifications"] }) });
  const markAll = useMutation({ mutationFn: () => api.post("/notifications/read-all"), onSuccess: () => client.invalidateQueries({ queryKey: ["notifications"] }) });
  const visit = async (notification: Notification) => { if (!notification.isRead) await markRead.mutateAsync(notification.id); setOpen(false); router.push(target(notification)); };
  return <div className="notification-bell"><button className="icon-button" aria-label="Notifications" aria-expanded={open} onClick={() => setOpen((value) => !value)}><Bell size={20} />{Boolean(count.data?.unreadCount) && <span className="notification-count">{Math.min(count.data?.unreadCount ?? 0, 99)}</span>}</button>{open && <div className="notification-popover"><header><div><strong>Notifications</strong><span>{count.data?.unreadCount ?? 0} unread</span></div><Button variant="ghost" size="sm" disabled={!count.data?.unreadCount || markAll.isPending} onClick={() => markAll.mutate()}><CheckCheck size={15} /> Mark all read</Button></header><div className="notification-items">{list.isPending ? <LoadingSkeleton rows={4} /> : list.error ? <ErrorState error={list.error} onRetry={() => list.refetch()} /> : !list.data?.items.length ? <EmptyState title="You’re all caught up" description="New reminders and escalations will appear here." /> : list.data.items.map((item) => <button key={item.id} className={`notification-item ${item.isRead ? "read" : "unread"}`} onClick={() => void visit(item)}><div><strong>{item.title}</strong><StatusBadge value={item.priority} /></div><p>{item.message}</p><time>{formatDateTime(item.createdAt)}</time></button>)}</div><footer><button onClick={() => { setOpen(false); router.push("/notifications"); }}>View all notifications</button></footer></div>}</div>;
}
