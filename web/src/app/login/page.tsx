"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { LoginForm } from "@/features/auth/login-form";
import { useAuth } from "@/features/auth/auth-provider";

export default function LoginPage() {
  const { status, user } = useAuth();
  const router = useRouter();
  useEffect(() => { if (status === "authenticated" && user?.isActive) router.replace("/dashboard"); }, [status, user, router]);
  return <main className="login-page"><section className="login-brand"><div className="brand-mark large"><span>M</span></div><p className="brand-kicker">MaintainPro</p><h2>Keep every asset ready.</h2><p>One clear view of preventive maintenance, compliance, breakdown response, and the work that needs attention today.</p><div className="onam-orbit" aria-hidden="true">{Array.from({ length: 8 }, (_, index) => <i key={index} />)}</div><div className="login-stat"><strong>Operational clarity</strong><span>Built for maintenance leaders</span></div></section><section className="login-panel"><LoginForm /><footer>Secure access · Activity is audited</footer></section></main>;
}
