"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

export default function Home() {
  const router = useRouter();
  useEffect(() => { router.replace("/dashboard"); }, [router]);
  return <main className="center-screen"><div className="brand-mark"><span>M</span></div><p>Opening MaintainPro…</p></main>;
}
