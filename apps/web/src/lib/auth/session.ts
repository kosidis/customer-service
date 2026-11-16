import "server-only";
import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/http";
import type { CurrentUser, Role } from "@/lib/api/types";

export const SESSION_COOKIE = "session";

const ONE_YEAR_SECONDS = 31536000;

export async function getSessionToken(): Promise<string | undefined> {
  const store = await cookies();
  return store.get(SESSION_COOKIE)?.value;
}

export async function setSessionCookie(token: string): Promise<void> {
  const store = await cookies();
  store.set(SESSION_COOKIE, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.SESSION_COOKIE_SECURE === "true",
    maxAge: ONE_YEAR_SECONDS,
    path: "/",
  });
}

export async function clearSessionCookie(): Promise<void> {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
}

// cache() keys on argument identity, so only the no-argument fetch is cached;
// the options are applied outside it. null means "not authenticated".
export const fetchCurrentUser = cache(async (): Promise<CurrentUser | null> => {
  if (!(await getSessionToken())) return null;
  try {
    return await getCurrentUser();
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
});

export async function requireUser(opts?: {
  allowPendingChange?: boolean;
  role?: Role;
}): Promise<CurrentUser> {
  const user = await fetchCurrentUser();
  if (!user) redirect("/login");
  if (user.mustChangePassword && !opts?.allowPendingChange) {
    redirect("/account/password");
  }
  if (opts?.role && user.role !== opts.role) redirect("/customers");
  return user;
}
