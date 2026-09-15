"use client";

import { CheckCircle2, CircleAlert, Info, X } from "lucide-react";
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";

type ToastTone = "success" | "error" | "info";
interface ToastItem { id: number; message: string; tone: ToastTone }
interface ToastContextValue { showToast: (message: string, tone?: ToastTone) => void }
const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const showToast = useCallback((message: string, tone: ToastTone = "success") => {
    const id = Date.now() + Math.random();
    setItems((current) => [...current, { id, message, tone }]);
    window.setTimeout(() => setItems((current) => current.filter((item) => item.id !== id)), 4200);
  }, []);
  const value = useMemo(() => ({ showToast }), [showToast]);
  return <ToastContext.Provider value={value}>{children}<div className="toast-region" aria-live="polite" aria-label="Notifications">{items.map((item) => <div className={`toast toast-${item.tone}`} key={item.id} role={item.tone === "error" ? "alert" : "status"}>{item.tone === "success" ? <CheckCircle2 size={18} /> : item.tone === "error" ? <CircleAlert size={18} /> : <Info size={18} />}<span>{item.message}</span><button aria-label="Dismiss" onClick={() => setItems((current) => current.filter((toast) => toast.id !== item.id))}><X size={16} /></button></div>)}</div></ToastContext.Provider>;
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used within ToastProvider");
  return context;
}
