import { api } from "./client";
import {
  AuthResponseSchema,
  type AuthResponse,
  type LoginRequest,
  type RegisterRequest,
} from "../schemas";

export function register(body: RegisterRequest): Promise<AuthResponse> {
  return api(
    "/api/auth/register",
    { method: "POST", body: JSON.stringify(body) },
    AuthResponseSchema
  );
}

export function login(body: LoginRequest): Promise<AuthResponse> {
  return api(
    "/api/auth/login",
    { method: "POST", body: JSON.stringify(body) },
    AuthResponseSchema
  );
}
