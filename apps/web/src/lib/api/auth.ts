import "server-only";
import { apiFetch } from "./http";
import type {
  ChangePasswordRequest,
  CurrentUser,
  LoginRequest,
  LoginResponse,
} from "./types";

export function login(request: LoginRequest): Promise<LoginResponse> {
  return apiFetch<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: request,
    authenticated: false,
  });
}

export async function logout(): Promise<void> {
  await apiFetch<void>("/api/auth/logout", { method: "POST" });
}

export function getCurrentUser(): Promise<CurrentUser> {
  return apiFetch<CurrentUser>("/api/auth/me");
}

export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  await apiFetch<void>("/api/auth/change-password", { method: "POST", body: request });
}
