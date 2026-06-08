import { useState, type FormEvent } from "react";
import { ApiException } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { LoginRequestSchema, RegisterRequestSchema } from "../schemas";

type Mode = "login" | "register";

export function AuthScreen() {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<Mode>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError(null);

    const schema = mode === "login" ? LoginRequestSchema : RegisterRequestSchema;
    const parsed = schema.safeParse({ email, password });
    if (!parsed.success) {
      const errs: Record<string, string[]> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0];
        if (typeof key !== "string") continue;
        const pascal = key.charAt(0).toUpperCase() + key.slice(1);
        (errs[pascal] ??= []).push(issue.message);
      }
      setFieldErrors(errs);
      return;
    }
    setFieldErrors({});

    setSubmitting(true);
    try {
      if (mode === "login") await login(email, password);
      else await register(email, password);
    } catch (e) {
      if (e instanceof ApiException) {
        if (e.error.errors) setFieldErrors(e.error.errors);
        else setFormError(e.error.detail ?? e.error.title);
      } else if (e instanceof Error) {
        setFormError(e.message);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-screen">
      <div className="auth-card">
        <h1>Tasks</h1>
        <h2>{mode === "login" ? "Sign in" : "Create an account"}</h2>

        <form onSubmit={onSubmit} noValidate className="task-form">
          <div className="field">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              aria-invalid={!!fieldErrors.Email}
            />
            {fieldErrors.Email?.map((m) => (
              <span key={m} className="field-error">{m}</span>
            ))}
          </div>

          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              autoComplete={mode === "login" ? "current-password" : "new-password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              aria-invalid={!!fieldErrors.Password}
            />
            {fieldErrors.Password?.map((m) => (
              <span key={m} className="field-error">{m}</span>
            ))}
          </div>

          {formError && <div className="form-error">{formError}</div>}

          <div className="form-actions">
            <button type="submit" disabled={submitting}>
              {submitting ? "…" : mode === "login" ? "Sign in" : "Create account"}
            </button>
          </div>
        </form>

        <p className="auth-switch">
          {mode === "login" ? (
            <>New here? <button type="button" className="link" onClick={() => setMode("register")}>Create an account</button></>
          ) : (
            <>Already have one? <button type="button" className="link" onClick={() => setMode("login")}>Sign in</button></>
          )}
        </p>
      </div>
    </div>
  );
}
