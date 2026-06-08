import type { ZodType } from "zod";
import { clearSession, getSession } from "../auth/token";
import type { ApiError } from "../schemas";
import { ConflictProblemSchema } from "../schemas";

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5154";

// Subscribers notified when the server signals the session is no longer valid
// (401 from a request we sent with a token). The AuthContext subscribes and
// clears its in-memory state, dropping the user back to the login screen.
type UnauthorizedHandler = () => void;
const unauthorizedHandlers = new Set<UnauthorizedHandler>();
export function onUnauthorized(fn: UnauthorizedHandler): () => void {
  unauthorizedHandlers.add(fn);
  return () => unauthorizedHandlers.delete(fn);
}

export class ApiException extends Error {
  readonly error: ApiError;
  constructor(error: ApiError) {
    super(error.detail ?? error.title);
    this.error = error;
  }
}

export async function api<T>(
  path: string,
  init: RequestInit = {},
  schema?: ZodType<T>
): Promise<T> {
  const session = getSession();
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(session ? { Authorization: `Bearer ${session.token}` } : {}),
      ...(init.headers ?? {}),
    },
  });

  if (res.status === 401 && session) {
    // Token rejected (expired or invalidated). Drop the session and let
    // subscribers (AuthContext) react.
    clearSession();
    for (const fn of unauthorizedHandlers) fn();
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const body: unknown = text ? JSON.parse(text) : undefined;

  if (!res.ok) throw new ApiException(toApiError(res.status, body));

  if (!schema) return body as T;
  return schema.parse(body);
}

function toApiError(status: number, body: unknown): ApiError {
  const obj = (body ?? {}) as Record<string, unknown>;
  const base: ApiError = {
    status,
    title: typeof obj.title === "string" ? obj.title : `HTTP ${status}`,
    detail: typeof obj.detail === "string" ? obj.detail : undefined,
    errors:
      obj.errors && typeof obj.errors === "object"
        ? (obj.errors as Record<string, string[]>)
        : undefined,
  };

  if (status === 409) {
    const conflict = ConflictProblemSchema.safeParse(body);
    if (conflict.success) base.current = conflict.data.current;
  }
  return base;
}
