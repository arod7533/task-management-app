import type { ZodType } from "zod";
import type { ApiError } from "../schemas";
import { ConflictProblemSchema } from "../schemas";

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5154";

export class ApiException extends Error {
  readonly error: ApiError;
  constructor(error: ApiError) {
    super(error.detail ?? error.title);
    this.error = error;
  }
}

// Generic JSON fetch wrapper. When `schema` is supplied, the successful body
// is parsed through it — any wire-format mismatch (renamed field, missing
// property, wrong type) throws at the boundary with a clear Zod message
// instead of corrupting state downstream.
export async function api<T>(
  path: string,
  init: RequestInit = {},
  schema?: ZodType<T>
): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init.headers ?? {}),
    },
  });

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

  // Concurrency conflicts carry the server's current state. Parse it through
  // the same TaskSchema so we know the rest of the app can safely render it.
  if (status === 409) {
    const conflict = ConflictProblemSchema.safeParse(body);
    if (conflict.success) base.current = conflict.data.current;
  }
  return base;
}
