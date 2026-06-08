// localStorage-backed token store. Chosen for simplicity and zero CSRF
// surface; trade-off is XSS exposure (covered in the README).

const KEY = "tm.token";
const EMAIL_KEY = "tm.email";

type Session = { token: string; email: string };

export function getSession(): Session | null {
  const token = localStorage.getItem(KEY);
  const email = localStorage.getItem(EMAIL_KEY);
  if (!token || !email) return null;
  return { token, email };
}

export function setSession(s: Session): void {
  localStorage.setItem(KEY, s.token);
  localStorage.setItem(EMAIL_KEY, s.email);
}

export function clearSession(): void {
  localStorage.removeItem(KEY);
  localStorage.removeItem(EMAIL_KEY);
}
