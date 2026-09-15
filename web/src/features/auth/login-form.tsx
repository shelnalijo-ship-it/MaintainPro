"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, Eye, EyeOff, LockKeyhole, UserRound } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button, Field, Input } from "@/components/ui";
import { errorMessage } from "@/lib/api-client";
import { useAuth } from "./auth-provider";

const schema = z.object({ identifier: z.string().trim().min(1, "Enter your email or employee ID"), password: z.string().min(1, "Enter your password") });
type FormValues = z.infer<typeof schema>;

export function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const [showPassword, setShowPassword] = useState(false);
  const [submitError, setSubmitError] = useState<string>();
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { identifier: "", password: "" } });
  const submit = handleSubmit(async (values) => {
    setSubmitError(undefined);
    try {
      const user = await login(values.identifier, values.password);
      if (!user.isActive) { setSubmitError("This account is inactive. Contact an administrator."); return; }
      if (!user.roles.some((role) => role === "MANAGER" || role === "ADMIN" || role === "SUPERVISOR")) { setSubmitError("Your account does not have web portal access."); return; }
      router.replace("/dashboard");
    } catch (error) { setSubmitError(errorMessage(error)); }
  });
  return <form className="login-form" onSubmit={submit} noValidate><div><span className="eyebrow">Manager web portal</span><h1>Welcome back</h1><p>Sign in to review maintenance, compliance, and operations.</p></div>{submitError && <div className="form-alert" role="alert">{submitError}</div>}<Field label="Email or employee ID" required error={errors.identifier?.message}><div className="input-with-icon"><UserRound size={18} /><Input autoComplete="username" autoFocus {...register("identifier")} /></div></Field><Field label="Password" required error={errors.password?.message}><div className="input-with-icon"><LockKeyhole size={18} /><Input type={showPassword ? "text" : "password"} autoComplete="current-password" {...register("password")} /><button className="password-toggle" type="button" aria-label={showPassword ? "Hide password" : "Show password"} onClick={() => setShowPassword((value) => !value)}>{showPassword ? <EyeOff size={18} /> : <Eye size={18} />}</button></div></Field><Button type="submit" loading={isSubmitting}>Sign in <ArrowRight size={17} /></Button><p className="login-help">Use the credentials provided by your MaintainPro administrator.</p></form>;
}
