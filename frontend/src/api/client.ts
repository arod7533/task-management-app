import type { ApiError } from "../types";

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5154";

export class ApiException extends Error {
  readonly error: ApiError;
  constructor(error: ApiError) {
    super(error.detail ?? error.title);
    this.error = error;
  }
}

export async function api<T>(
  path: string,
  init: RequestInit = {}
): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init.headers ?? {}),
    },
  });

  if (res.status === 204) {
    return undefined as T;
  }

  const text = await res.text();
  const body = text ? JSON.parse(text) : undefined;

  if (!res.ok) {
    throw new ApiException({
      status: res.status,
      title: body?.title ?? res.statusText,
      detail: body?.detail,
      errors: body?.errors,
      current: body?.current,
    });
  }

  return body as T;
}
